using System.Collections.ObjectModel;
using System.IO;
using WindowsToolbox.Core.Commands;
using WindowsToolbox.Core.Utilities;
using WindowsToolbox.Modules.FileTools.Models;
using WindowsToolbox.Modules.FileTools.Services;

namespace WindowsToolbox.Modules.FileTools.ViewModels;

public sealed class FileToolsViewModel : ObservableObject, IDisposable
{
    private readonly FileSelectionService _selection = new();
    private readonly RenameService _renameService;
    private readonly HashService _hashService;
    private readonly PathToolsService _pathService;
    private readonly FileInfoService _infoService;
    private CancellationTokenSource? _operationCancellation;
    private FileToolTab _selectedTool = FileToolTab.Rename;
    private string _prefix = string.Empty;
    private string _suffix = string.Empty;
    private string _find = string.Empty;
    private string _replace = string.Empty;
    private bool _matchCase;
    private bool _numberingEnabled;
    private int _numberStart = 1;
    private int _numberStep = 1;
    private int _numberDigits = 3;
    private string _numberPrefix = string.Empty;
    private string _numberSuffix = "_";
    private RenameCaseMode _caseMode;
    private string _newExtension = string.Empty;
    private string _selectedAlgorithm = "SHA-256";
    private string _expectedHash = string.Empty;
    private string _pathInput = string.Empty;
    private string _status = "选择文件或文件夹开始处理";
    private string _error = string.Empty;
    private string _hashProgressText = string.Empty;
    private double _currentProgress;
    private double _overallProgress;
    private bool _isBusy;
    private FileInfoSnapshot? _selectedInfo;
    private FolderSizeResult? _folderSize;
    private string _verificationResult = string.Empty;

    public FileToolsViewModel(RenameService renameService, HashService hashService, PathToolsService pathService, FileInfoService infoService)
    {
        _renameService = renameService;
        _hashService = hashService;
        _pathService = pathService;
        _infoService = infoService;
        RenamePreviewCommand = new RelayCommand(RefreshRenamePreview);
        ApplyRenameCommand = new AsyncRelayCommand(ApplyRenameAsync, () => CanApplyRename);
        UndoRenameCommand = new AsyncRelayCommand(UndoRenameAsync);
        ComputeHashCommand = new AsyncRelayCommand(ComputeHashAsync, () => !IsBusy && SelectedPaths.Count > 0);
        CancelCommand = new RelayCommand(CancelCurrentOperation, () => IsBusy);
        CalculateFolderSizeCommand = new AsyncRelayCommand(CalculateFolderSizeAsync, () => !IsBusy && SelectedInfo?.IsDirectory == true);
    }

    public ObservableCollection<string> SelectedPaths { get; } = [];
    public ObservableCollection<RenamePreviewItem> RenamePreview { get; } = [];
    public ObservableCollection<HashResult> HashResults { get; } = [];
    public ObservableCollection<PathResult> PathResults { get; } = [];
    public IReadOnlyList<string> HashAlgorithms => HashAlgorithmCatalog.Names;
    public IReadOnlyList<RenameCaseMode> CaseModes { get; } = Enum.GetValues<RenameCaseMode>();
    public FileToolTab SelectedTool { get => _selectedTool; private set { if (SetProperty(ref _selectedTool, value)) OnPropertyChanged(nameof(SelectedToolText)); } }
    public string SelectedToolText => SelectedTool switch { FileToolTab.Rename => "批量重命名", FileToolTab.Hash => "文件校验", FileToolTab.Path => "路径工具", _ => "文件信息" };
    public string Prefix { get => _prefix; set { if (SetProperty(ref _prefix, value ?? string.Empty)) RefreshRenamePreview(); } }
    public string Suffix { get => _suffix; set { if (SetProperty(ref _suffix, value ?? string.Empty)) RefreshRenamePreview(); } }
    public string Find { get => _find; set { if (SetProperty(ref _find, value ?? string.Empty)) RefreshRenamePreview(); } }
    public string Replace { get => _replace; set { if (SetProperty(ref _replace, value ?? string.Empty)) RefreshRenamePreview(); } }
    public bool MatchCase { get => _matchCase; set { if (SetProperty(ref _matchCase, value)) RefreshRenamePreview(); } }
    public bool NumberingEnabled { get => _numberingEnabled; set { if (SetProperty(ref _numberingEnabled, value)) RefreshRenamePreview(); } }
    public int NumberStart { get => _numberStart; set { if (SetProperty(ref _numberStart, value)) RefreshRenamePreview(); } }
    public int NumberStep { get => _numberStep; set { if (SetProperty(ref _numberStep, value)) RefreshRenamePreview(); } }
    public int NumberDigits { get => _numberDigits; set { if (SetProperty(ref _numberDigits, value)) RefreshRenamePreview(); } }
    public string NumberPrefix { get => _numberPrefix; set { if (SetProperty(ref _numberPrefix, value ?? string.Empty)) RefreshRenamePreview(); } }
    public string NumberSuffix { get => _numberSuffix; set { if (SetProperty(ref _numberSuffix, value ?? string.Empty)) RefreshRenamePreview(); } }
    public RenameCaseMode CaseMode { get => _caseMode; set { if (SetProperty(ref _caseMode, value)) RefreshRenamePreview(); } }
    public string NewExtension { get => _newExtension; set { if (SetProperty(ref _newExtension, value ?? string.Empty)) RefreshRenamePreview(); } }
    public string SelectedAlgorithm { get => _selectedAlgorithm; set => SetProperty(ref _selectedAlgorithm, value ?? "SHA-256"); }
    public string ExpectedHash { get => _expectedHash; set { if (SetProperty(ref _expectedHash, value ?? string.Empty)) OnPropertyChanged(nameof(VerificationText)); } }
    public string PathInput { get => _pathInput; set { if (SetProperty(ref _pathInput, value ?? string.Empty)) ConvertPaths(); } }
    public string Status { get => _status; private set => SetProperty(ref _status, value); }
    public string Error { get => _error; private set => SetProperty(ref _error, value); }
    public string HashProgressText { get => _hashProgressText; private set => SetProperty(ref _hashProgressText, value); }
    public double CurrentProgress { get => _currentProgress; private set => SetProperty(ref _currentProgress, value); }
    public double OverallProgress { get => _overallProgress; private set => SetProperty(ref _overallProgress, value); }
    public bool IsBusy { get => _isBusy; private set { if (SetProperty(ref _isBusy, value)) { ComputeHashCommand.NotifyCanExecuteChanged(); CancelCommand.NotifyCanExecuteChanged(); CalculateFolderSizeCommand.NotifyCanExecuteChanged(); } } }
    public FileInfoSnapshot? SelectedInfo { get => _selectedInfo; private set { if (SetProperty(ref _selectedInfo, value)) CalculateFolderSizeCommand.NotifyCanExecuteChanged(); } }
    public FolderSizeResult? FolderSize { get => _folderSize; private set => SetProperty(ref _folderSize, value); }
    public string VerificationText => string.IsNullOrWhiteSpace(ExpectedHash) ? string.Empty : "输入期望 Hash 后可对选中文件进行比较";
    public string VerificationResult { get => _verificationResult; private set => SetProperty(ref _verificationResult, value); }
    public bool CanApplyRename => !IsBusy && RenamePreview.Count > 0 && RenamePreview.All(item => item.Status is RenameItemStatus.Ready or RenameItemStatus.Unchanged) && RenamePreview.Any(item => item.Status == RenameItemStatus.Ready);

    public RelayCommand RenamePreviewCommand { get; }
    public AsyncRelayCommand ApplyRenameCommand { get; }
    public AsyncRelayCommand UndoRenameCommand { get; }
    public AsyncRelayCommand ComputeHashCommand { get; }
    public RelayCommand CancelCommand { get; }
    public AsyncRelayCommand CalculateFolderSizeCommand { get; }

    public void SelectTool(FileToolTab tab)
    {
        SelectedTool = tab;
        Error = string.Empty;
        if (tab == FileToolTab.Info && SelectedPaths.Count > 0)
            LoadSelectedInfo();
        if (tab == FileToolTab.Path && !string.IsNullOrWhiteSpace(PathInput))
            ConvertPaths();
    }

    public void AddPaths(IEnumerable<string> paths)
    {
        _selection.Add(paths);
        SyncSelection();
        RefreshRenamePreview();
        if (SelectedTool == FileToolTab.Info)
            LoadSelectedInfo();
        if (SelectedTool == FileToolTab.Path)
            PathInput = SelectedPaths.FirstOrDefault() ?? PathInput;
        Status = $"已选择 {SelectedPaths.Count} 个项目";
    }

    public void RemovePath(string? path)
    {
        if (!string.IsNullOrWhiteSpace(path))
            _selection.Remove(path);
        SyncSelection();
        RefreshRenamePreview();
    }

    public void ClearPaths()
    {
        _selection.Clear();
        SyncSelection();
        RenamePreview.Clear();
        HashResults.Clear();
        PathResults.Clear();
        SelectedInfo = null;
        FolderSize = null;
        Status = "已清空文件列表";
        NotifyCommands();
    }

    public void LoadSelectedInfo()
    {
        string? path = SelectedPaths.FirstOrDefault();
        if (path is null) { SelectedInfo = null; return; }
        try { SelectedInfo = _infoService.Read(path); Status = SelectedInfo.Exists ? "已读取文件信息" : "项目不存在"; Error = string.Empty; }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException or ArgumentException)
        { Error = "读取文件信息失败：" + exception.Message; }
    }

    public void RefreshRenamePreview()
    {
        try
        {
            RenamePreview.Clear();
            if (SelectedPaths.Count == 0) { NotifyCommands(); return; }
            foreach (RenamePreviewItem item in _renameService.BuildPreview(SelectedPaths, BuildRenameOptions()))
                RenamePreview.Add(item);
            Error = string.Empty;
            NotifyCommands();
        }
        catch (Exception exception) when (exception is IOException or ArgumentException)
        {
            Error = "生成预览失败：" + exception.Message;
        }
    }

    public async Task ApplyRenameAsync()
    {
        if (!CanApplyRename) return;
        IsBusy = true;
        try
        {
            RenameBatchResult result = await _renameService.ApplyAsync(RenamePreview.ToArray()).ConfigureAwait(true);
            Status = result.Message;
            Error = result.Failed == 0 ? string.Empty : $"成功 {result.Succeeded} 个，失败 {result.Failed} 个。";
            RefreshRenamePreview();
        }
        catch (OperationCanceledException) { Status = "重命名已取消"; }
        finally { IsBusy = false; NotifyCommands(); }
    }

    public async Task UndoRenameAsync()
    {
        IsBusy = true;
        try
        {
            RenameBatchResult result = await _renameService.UndoLastBatchAsync().ConfigureAwait(true);
            Status = result.Message;
            Error = result.Failed == 0 ? string.Empty : "无法安全撤销，部分文件需要人工处理。";
            RefreshRenamePreview();
        }
        finally { IsBusy = false; NotifyCommands(); }
    }

    public async Task ComputeHashAsync()
    {
        if (SelectedPaths.Count == 0) { Error = "请先选择文件。"; return; }
        CancelCurrentOperation();
        _operationCancellation = new CancellationTokenSource();
        IsBusy = true;
        HashResults.Clear();
        CurrentProgress = 0;
        OverallProgress = 0;
        HashProgressText = "准备计算…";
        try
        {
            Progress<HashProgress> progress = new(value =>
            {
                CurrentProgress = value.CurrentFraction * 100;
                OverallProgress = value.OverallFraction * 100;
                HashProgressText = $"{value.CurrentIndex}/{value.TotalFiles} · {Path.GetFileName(value.CurrentFile)} · {value.CurrentBytes:N0}/{value.CurrentLength:N0} B";
            });
            IReadOnlyList<HashResult> results = await _hashService.ComputeAsync(SelectedPaths, SelectedAlgorithm, progress, _operationCancellation.Token).ConfigureAwait(true);
            foreach (HashResult result in results) HashResults.Add(result);
            Status = $"已完成 {results.Count} 个文件的 {SelectedAlgorithm} 校验";
            Error = string.Empty;
        }
        catch (OperationCanceledException) { Status = "Hash 计算已取消"; Error = string.Empty; }
        catch (Exception exception) { Error = "Hash 计算失败：" + exception.Message; }
        finally { IsBusy = false; _operationCancellation?.Dispose(); _operationCancellation = null; NotifyCommands(); }
    }

    public void VerifyExpectedHash()
    {
        if (string.IsNullOrWhiteSpace(ExpectedHash))
        {
            VerificationResult = "请输入 Expected Hash。";
            return;
        }
        HashResult? result = HashResults.FirstOrDefault(item => item.Status == HashItemStatus.Completed);
        if (result is null)
        {
            VerificationResult = "请先完成至少一个文件的 Hash 计算。";
            return;
        }
        VerificationResult = HashService.Verify(ExpectedHash, result.Hash) ? "Match：Hash 一致" : "Mismatch：Hash 不一致";
    }

    public async Task GenerateSha256SumsAsync(string path, bool overwrite)
    {
        try
        {
            await _hashService.GenerateSha256SumsAsync(HashResults, path, overwrite).ConfigureAwait(true);
            Status = "已生成 SHA256SUMS.txt";
            Error = string.Empty;
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            Error = "生成校验清单失败：" + exception.Message;
        }
    }

    public void CancelCurrentOperation()
    {
        _operationCancellation?.Cancel();
        Status = "正在取消…";
    }

    public void ConvertPaths()
    {
        string[] inputs = string.IsNullOrWhiteSpace(PathInput) ? SelectedPaths.ToArray() : PathInput.Split(['\r', '\n'], StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        if (inputs.Length == 0) { PathResults.Clear(); return; }
        try
        {
            PathResults.Clear();
            foreach (PathResult result in _pathService.Convert(inputs, Enum.GetValues<PathOutputKind>()))
                PathResults.Add(result);
            Error = string.Empty;
        }
        catch (ArgumentException exception) { Error = "路径格式无效：" + exception.Message; }
    }

    public async Task CalculateFolderSizeAsync()
    {
        if (SelectedInfo?.IsDirectory != true) { Error = "请选择文件夹后再计算大小。"; return; }
        CancelCurrentOperation();
        _operationCancellation = new CancellationTokenSource();
        IsBusy = true;
        try
        {
            Progress<FolderSizeProgress> progress = new(value => Status = $"扫描中：文件 {value.FilesScanned:N0} 个，目录 {value.DirectoriesScanned:N0} 个，跳过 {value.Skipped:N0} 项");
            FolderSize = await _infoService.CalculateFolderSizeAsync(SelectedInfo.Path, progress, _operationCancellation.Token).ConfigureAwait(true);
            Status = FolderSize.Summary;
            Error = string.Empty;
        }
        catch (OperationCanceledException) { Status = "文件夹扫描已取消"; }
        catch (Exception exception) { Error = "文件夹扫描失败：" + exception.Message; }
        finally { IsBusy = false; _operationCancellation?.Dispose(); _operationCancellation = null; NotifyCommands(); }
    }

    private void SyncSelection()
    {
        SelectedPaths.Clear();
        foreach (string path in _selection.Items) SelectedPaths.Add(path);
        OnPropertyChanged(nameof(CanApplyRename));
    }

    private RenameRuleOptions BuildRenameOptions() => new()
    {
        Prefix = Prefix,
        Suffix = Suffix,
        Find = Find,
        Replace = Replace,
        MatchCase = MatchCase,
        NumberingEnabled = NumberingEnabled,
        NumberStart = NumberStart,
        NumberStep = NumberStep,
        NumberDigits = NumberDigits,
        NumberPrefix = NumberPrefix,
        NumberSuffix = NumberSuffix,
        CaseMode = CaseMode,
        NewExtension = NewExtension
    };

    private void NotifyCommands()
    {
        OnPropertyChanged(nameof(CanApplyRename));
        ApplyRenameCommand.NotifyCanExecuteChanged();
        ComputeHashCommand.NotifyCanExecuteChanged();
        CancelCommand.NotifyCanExecuteChanged();
        CalculateFolderSizeCommand.NotifyCanExecuteChanged();
    }

    public void Dispose() { CancelCurrentOperation(); _operationCancellation?.Dispose(); }
}
