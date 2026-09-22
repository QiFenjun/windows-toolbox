using System.Collections.ObjectModel;
using WindowsToolbox.Core.Commands;
using WindowsToolbox.Core.Utilities;
using WindowsToolbox.Modules.TextTools.Models;
using WindowsToolbox.Modules.TextTools.Services;

namespace WindowsToolbox.Modules.TextTools.ViewModels;

public sealed class TextToolsViewModel : ObservableObject, IDisposable
{
    private readonly ITextClipboardAdapter _clipboard;
    private readonly IReadOnlyList<ITextOperation> _operations = TextOperationRegistry.CreateDefault();
    private CancellationTokenSource? _processingCancellation;
    private int _generation;
    private ITextOperation? _selectedOperation;
    private string _operationSearch = string.Empty;
    private string _inputText = string.Empty;
    private string _outputText = string.Empty;
    private string _prefix = string.Empty;
    private string _suffix = string.Empty;
    private string _find = string.Empty;
    private string _replace = string.Empty;
    private bool _matchCase;
    private bool _isLive = true;
    private string _status = "输入文本后将自动处理";
    private string _error = string.Empty;

    public TextToolsViewModel(ITextClipboardAdapter clipboard)
    {
        _clipboard = clipboard;
        FilteredOperations = new ObservableCollection<ITextOperation>(_operations);
        SelectedOperation = FilteredOperations.FirstOrDefault();
        ExecuteCommand = new AsyncRelayCommand(() => ProcessAsync(false));
        CopyOutputCommand = new RelayCommand(CopyOutput);
        ReplaceInputCommand = new RelayCommand(() => { if (!string.IsNullOrEmpty(OutputText)) InputText = OutputText; });
        ClearCommand = new RelayCommand(Clear);
        SwapCommand = new RelayCommand(Swap);
        PasteClipboardCommand = new RelayCommand(PasteClipboard);
    }

    public ObservableCollection<ITextOperation> FilteredOperations { get; }
    public ITextOperation? SelectedOperation
    {
        get => _selectedOperation;
        set
        {
            if (!SetProperty(ref _selectedOperation, value)) return;
            OnPropertyChanged(nameof(IsFindReplace));
            ScheduleLiveProcess();
        }
    }
    public string OperationSearch
    {
        get => _operationSearch;
        set
        {
            if (!SetProperty(ref _operationSearch, value)) return;
            FilteredOperations.Clear();
            foreach (ITextOperation operation in _operations.Where(operation =>
                         string.IsNullOrWhiteSpace(value) ||
                         operation.DisplayName.Contains(value, StringComparison.CurrentCultureIgnoreCase) ||
                         operation.Description.Contains(value, StringComparison.CurrentCultureIgnoreCase) ||
                         operation.Category.Contains(value, StringComparison.CurrentCultureIgnoreCase)))
                FilteredOperations.Add(operation);
        }
    }
    public string InputText { get => _inputText; set { if (SetProperty(ref _inputText, value ?? string.Empty)) ScheduleLiveProcess(); } }
    public string OutputText { get => _outputText; private set { if (SetProperty(ref _outputText, value)) { OnPropertyChanged(nameof(OutputLength)); OutputStatistics = TextStatistics.From(value); } } }
    public string Prefix { get => _prefix; set { if (SetProperty(ref _prefix, value ?? string.Empty)) ScheduleLiveProcess(); } }
    public string Suffix { get => _suffix; set { if (SetProperty(ref _suffix, value ?? string.Empty)) ScheduleLiveProcess(); } }
    public string FindText { get => _find; set { if (SetProperty(ref _find, value ?? string.Empty)) ScheduleLiveProcess(); } }
    public string ReplaceText { get => _replace; set { if (SetProperty(ref _replace, value ?? string.Empty)) ScheduleLiveProcess(); } }
    public bool MatchCase { get => _matchCase; set { if (SetProperty(ref _matchCase, value)) ScheduleLiveProcess(); } }
    public bool IsLive { get => _isLive; private set => SetProperty(ref _isLive, value); }
    public bool IsFindReplace => string.Equals(SelectedOperation?.Id, "find-replace", StringComparison.Ordinal);
    public string Status { get => _status; private set => SetProperty(ref _status, value); }
    public string Error { get => _error; private set => SetProperty(ref _error, value); }
    public int InputLength => InputText.Length;
    public int OutputLength => OutputText.Length;
    public TextStatistics InputStatistics { get; private set; } = TextStatistics.From(string.Empty);
    public TextStatistics OutputStatistics { get; private set; } = TextStatistics.From(string.Empty);
    public AsyncRelayCommand ExecuteCommand { get; }
    public RelayCommand CopyOutputCommand { get; }
    public RelayCommand ReplaceInputCommand { get; }
    public RelayCommand ClearCommand { get; }
    public RelayCommand SwapCommand { get; }
    public RelayCommand PasteClipboardCommand { get; }

    private void ScheduleLiveProcess()
    {
        InputStatistics = TextStatistics.From(InputText);
        OnPropertyChanged(nameof(InputStatistics));
        OnPropertyChanged(nameof(InputLength));
        Error = string.Empty;
        IsLive = InputText.Length <= 1024 * 1024;
        if (!IsLive)
        {
            CancelProcessing();
            Status = "文本超过 1 MiB，请点击“处理”执行";
            return;
        }
        _ = ProcessAsync(true);
    }

    private async Task ProcessAsync(bool debounce)
    {
        ITextOperation? operation = SelectedOperation;
        if (operation is null) return;
        CancellationTokenSource previous = _processingCancellation ?? new CancellationTokenSource();
        previous.Cancel();
        previous.Dispose();
        CancellationTokenSource current = _processingCancellation = new();
        int generation = ++_generation;
        try
        {
            if (debounce) await Task.Delay(180, current.Token).ConfigureAwait(true);
            TextOperationResult result = await Task.Run(() => operation.Execute(InputText,
                new TextOperationOptions { Prefix = Prefix, Suffix = Suffix, Find = FindText, Replace = ReplaceText, MatchCase = MatchCase }), current.Token).ConfigureAwait(true);
            if (current.IsCancellationRequested || generation != _generation) return;
            if (result.IsSuccess)
            {
                OutputText = result.Output;
                Error = string.Empty;
                Status = result.Message ?? "处理完成";
            }
            else
            {
                Error = result.Error ?? "处理失败";
                Status = "处理失败，输出未修改";
            }
        }
        catch (OperationCanceledException) { }
        catch (Exception exception)
        {
            if (generation == _generation) { Error = exception.Message; Status = "处理失败，输出未修改"; }
        }
    }

    private void CopyOutput()
    {
        if (string.IsNullOrEmpty(OutputText)) { Error = "没有可复制的输出。"; return; }
        try { _clipboard.WriteText(OutputText); Status = "已复制输出"; Error = string.Empty; }
        catch { Error = "剪贴板不可用，请稍后重试。"; }
    }

    private void PasteClipboard()
    {
        try { string? value = _clipboard.ReadText(); if (value is not null) InputText = value; Status = value is null ? "剪贴板没有纯文本" : "已粘贴剪贴板文本"; }
        catch { Error = "读取剪贴板失败。"; }
    }

    private void Clear()
    {
        CancelProcessing(); InputText = string.Empty; OutputText = string.Empty; Error = string.Empty; Status = "已清空";
    }

    private void Swap()
    {
        if (string.IsNullOrEmpty(OutputText)) return;
        string input = InputText; InputText = OutputText; OutputText = input; Status = "已交换输入与输出";
    }

    private void CancelProcessing()
    {
        _processingCancellation?.Cancel();
        _generation++;
    }

    public void Dispose() { CancelProcessing(); _processingCancellation?.Dispose(); }
}
