using System.Text.RegularExpressions;
using System.Text;
using System.Windows.Input;
using WindowsToolbox.Core.Commands;
using WindowsToolbox.Core.Utilities;
using WindowsToolbox.Modules.Utilities.Regex.Models;
using WindowsToolbox.Modules.Utilities.Regex.Services;
using WindowsToolbox.Modules.Utilities.Services;

namespace WindowsToolbox.Modules.Utilities.Regex.ViewModels;

public sealed class RegexToolsViewModel : ObservableObject, IDisposable
{
    private readonly IUtilitiesTextClipboardAdapter _clipboard;
    private readonly Func<RegexRequest, CancellationToken, RegexResult> _run;
    private CancellationTokenSource? _current;
    private int _generation, _optionFlags, _timeoutIndex = 1;
    private string _pattern = "", _text = "", _replacement = "", _message = "输入 Pattern 与文本后运行；所有内容仅保留在当前会话。", _replacementPreview = "";
    private bool _live = true, _active, _disposed;
    private RegexResult _result = new(RegexRunStatus.Valid, [], "", "");
    private RegexMatchItem? _selectedMatch;

    public RegexToolsViewModel() : this(new WindowsUtilitiesTextClipboardAdapter()) { }
    public RegexToolsViewModel(IUtilitiesTextClipboardAdapter clipboard,
        Func<RegexRequest, CancellationToken, RegexResult>? runner = null)
    {
        _clipboard = clipboard ?? throw new ArgumentNullException(nameof(clipboard));
        _run = runner ?? RegexToolsService.Run;
        RunCommand = new AsyncRelayCommand(RunAsync, () => !_disposed);
        CancelCommand = new RelayCommand(Cancel, () => _current is not null);
        CopyCommand = new RelayCommand<string>(Copy, value => !string.IsNullOrEmpty(value));
    }

    public static IReadOnlyList<string> TimeoutChoices { get; } = ["100 ms", "500 ms", "1000 ms", "2000 ms"];
    public Task PendingTask { get; private set; } = Task.CompletedTask;
    public AsyncRelayCommand RunCommand { get; }
    public RelayCommand CancelCommand { get; }
    public RelayCommand<string> CopyCommand { get; }
    public IReadOnlyList<RegexMatchItem> Matches => _result.Matches;
    public RegexRunStatus StatusCode => _result.Status;
    public string StatusText => string.IsNullOrEmpty(_message) ? _result.Message : _message;
    public string SoftLimitText => Encoding.UTF8.GetByteCount(Text) > RegexToolsService.SoftInputBytes ? "文本超过 1 MiB，实时处理可能较慢。" : "";
    public string ReplacementPreview { get => _replacementPreview; private set => SetProperty(ref _replacementPreview, value); }
    public string Pattern { get => _pattern; set { if (SetProperty(ref _pattern, value ?? "")) InputsChanged(); } }
    public string Text { get => _text; set { if (SetProperty(ref _text, value ?? "")) InputsChanged(); } }
    public string Replacement { get => _replacement; set { if (SetProperty(ref _replacement, value ?? "")) InputsChanged(); } }
    public bool LiveUpdate { get => _live; set { if (SetProperty(ref _live, value)) InputsChanged(); } }
    public bool IsActive => _active;
    public bool LiveAllowed => RegexToolsService.AllowsLive(Text);
    public bool RequiresManualRun => !LiveAllowed;
    public int TimeoutIndex { get => _timeoutIndex; set { if (value is < 0 or > 3) return; if (SetProperty(ref _timeoutIndex, value)) InputsChanged(); } }
    public string CopyStatus { get; private set; } = "";
    public RegexMatchItem? SelectedMatch
    {
        get => _selectedMatch;
        set { if (SetProperty(ref _selectedMatch, value)) { OnPropertyChanged(nameof(SelectedGroups)); OnPropertyChanged(nameof(SelectedValue)); } }
    }
    public IReadOnlyList<RegexGroupItem> SelectedGroups => SelectedMatch?.Groups ?? [];
    public string SelectedValue => SelectedMatch?.Value ?? "";

    public bool GetOption(int index) => index is >= 0 and < 5 && (_optionFlags & (1 << index)) != 0;
    public void SetOption(int index, bool value)
    {
        if (index is < 0 or >= 5) return;
        int flag = 1 << index, updated = value ? _optionFlags | flag : _optionFlags & ~flag;
        if (_optionFlags == updated) return;
        _optionFlags = updated; OnPropertyChanged(nameof(Options)); InputsChanged();
    }
    public RegexOptions Options => (RegexOptions)_optionFlags;
    public bool IgnoreCase { get => GetOption(0); set => SetOption(0, value); }
    public bool Multiline { get => GetOption(1); set => SetOption(1, value); }
    public bool Singleline { get => GetOption(2); set => SetOption(2, value); }
    public bool CultureInvariant { get => GetOption(3); set => SetOption(3, value); }
    public bool ExplicitCapture { get => GetOption(4); set => SetOption(4, value); }

    public void Activate()
    {
        if (_disposed) return;
        _active = true; OnPropertyChanged(nameof(IsActive));
        if (LiveUpdate && LiveAllowed && (!string.IsNullOrEmpty(Pattern) || !string.IsNullOrEmpty(Text))) Schedule();
    }
    public void Deactivate()
    {
        _active = false; OnPropertyChanged(nameof(IsActive));
        CancelGeneration(clear: true);
    }
    private void InputsChanged()
    {
        OnPropertyChanged(nameof(LiveAllowed)); OnPropertyChanged(nameof(RequiresManualRun)); OnPropertyChanged(nameof(SoftLimitText));
        if (_disposed) return;
        CancelGeneration(clear: true);
        if (_active && LiveUpdate && LiveAllowed) Schedule();
        else if (!LiveAllowed) _message = "文本超过 5 MiB，不执行实时匹配；点击“运行”显式处理。";
    }
    private void CancelGeneration(bool clear)
    {
        ++_generation;
        _current?.Cancel(); _current = null; CancelCommand.NotifyCanExecuteChanged();
        if (!clear || _disposed) return;
        _result = new(RegexRunStatus.Valid, [], "", ""); ReplacementPreview = ""; SelectedMatch = null;
        _message = RequiresManualRun ? "文本超过 5 MiB，不执行实时匹配；点击“运行”显式处理。" : "结果已过期，正在等待新的输入。";
        NotifyResult();
    }
    private void Schedule()
    {
        int generation = _generation;
        CancellationTokenSource cancellation = new(); _current = cancellation;
        CancelCommand.NotifyCanExecuteChanged();
        PendingTask = ExecuteAsync(Snapshot(), generation, cancellation, TimeSpan.FromMilliseconds(250));
    }
    private void Cancel() => _current?.Cancel();
    public Task RunAsync()
    {
        if (_disposed) return Task.CompletedTask;
        CancelGeneration(clear: false);
        CancellationTokenSource cancellation = new(); int generation = _generation; _current = cancellation;
        CancelCommand.NotifyCanExecuteChanged();
        PendingTask = ExecuteAsync(Snapshot(), generation, cancellation, TimeSpan.Zero);
        return PendingTask;
    }
    private RegexRequest Snapshot() => new(Pattern, Text, Replacement, Options, new[] { 100, 500, 1000, 2000 }[TimeoutIndex]);

    private async Task ExecuteAsync(RegexRequest request, int generation, CancellationTokenSource cancellation, TimeSpan debounce)
    {
        try
        {
            if (debounce > TimeSpan.Zero) await Task.Delay(debounce, cancellation.Token);
            if (generation != _generation || _disposed) return;
            _message = "Running…"; OnPropertyChanged(nameof(StatusText));
            RegexResult result = await Task.Run(() => _run(request, cancellation.Token));
            if (generation != _generation || _disposed) return;
            _result = result; ReplacementPreview = result.Replacement; _message = ""; SelectedMatch = result.Matches.FirstOrDefault();
            NotifyResult();
        }
        catch (OperationCanceledException)
        {
            if (generation == _generation && !_disposed) { _result = new(RegexRunStatus.Cancelled, [], "", "已取消。"); ReplacementPreview = ""; NotifyResult(); }
        }
        catch (Exception)
        {
            if (generation == _generation && !_disposed) { _result = new(RegexRunStatus.Invalid, [], "", "正则处理失败。请检查输入后重试。"); ReplacementPreview = ""; NotifyResult(); }
        }
        finally
        {
            if (ReferenceEquals(_current, cancellation)) { _current = null; CancelCommand.NotifyCanExecuteChanged(); }
            cancellation.Dispose();
        }
    }
    private void NotifyResult()
    {
        OnPropertyChanged(nameof(Matches)); OnPropertyChanged(nameof(StatusCode)); OnPropertyChanged(nameof(StatusText));
        OnPropertyChanged(nameof(SelectedGroups)); OnPropertyChanged(nameof(SelectedValue)); RunCommand.NotifyCanExecuteChanged();
    }
    private void Copy(string? text)
    {
        if (string.IsNullOrEmpty(text)) return;
        try { _clipboard.SetText(text); CopyStatus = "已复制。"; }
        catch { CopyStatus = "无法访问剪贴板，请稍后重试。"; }
        OnPropertyChanged(nameof(CopyStatus));
    }
    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true; ++_generation; _current?.Cancel(); _current = null; CancelCommand.NotifyCanExecuteChanged();
        _pattern = _text = _replacement = "";
        OnPropertyChanged(nameof(Pattern)); OnPropertyChanged(nameof(Text)); OnPropertyChanged(nameof(Replacement));
        _result = new(RegexRunStatus.Valid, [], "", ""); ReplacementPreview = ""; SelectedMatch = null; NotifyResult();
    }
}
