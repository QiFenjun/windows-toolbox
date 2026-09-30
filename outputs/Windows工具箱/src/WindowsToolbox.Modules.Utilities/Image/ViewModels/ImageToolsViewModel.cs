using System.Collections.ObjectModel;
using System.Globalization;
using System.IO;
using System.Windows.Media.Imaging;
using WindowsToolbox.Core.Commands;
using WindowsToolbox.Core.Utilities;
using WindowsToolbox.Modules.Utilities.Image.Models;
using WindowsToolbox.Modules.Utilities.Image.Services;

namespace WindowsToolbox.Modules.Utilities.Image.ViewModels;

public sealed class ImageToolsViewModel : ObservableObject, IDisposable
{
    private readonly SemaphoreSlim _previewGate = new(1);
    private CancellationTokenSource? _batchCancellation;
    private int _previewGeneration;
    private bool _disposed, _isBusy, _aspectUpdating;
    private string _width = "1920", _height = "1080", _quality = "90", _outputDirectory = "", _status = "添加 PNG / JPEG / BMP 图片，输出默认为源目录中的新文件。";
    private bool _keepAspect = true, _noUpscale = true, _resize = true;
    private int _formatIndex;
    private ImageResult? _selectedItem;
    private ImageInfo? _info;
    private BitmapSource? _preview;

    public ImageToolsViewModel()
    {
        RunCommand = new AsyncRelayCommand(RunAsync, () => !IsBusy && Items.Count > 0 && !_disposed);
        CancelCommand = new RelayCommand(() => _batchCancellation?.Cancel());
        ClearCommand = new RelayCommand(Clear, () => !IsBusy);
    }

    public ObservableCollection<ImageResult> Items { get; } = [];
    public IReadOnlyList<string> Formats { get; } = ["PNG", "JPEG", "BMP"];
    public AsyncRelayCommand RunCommand { get; }
    public RelayCommand CancelCommand { get; }
    public RelayCommand ClearCommand { get; }
    public Task PreviewTask { get; private set; } = Task.CompletedTask;
    public bool IsBusy { get => _isBusy; private set { SetProperty(ref _isBusy, value); OnPropertyChanged(nameof(IsIdle)); RunCommand.NotifyCanExecuteChanged(); ClearCommand.NotifyCanExecuteChanged(); } }
    public bool IsIdle => !IsBusy;
    public string Status { get => _status; private set => SetProperty(ref _status, value); }
    public string WidthText { get => _width; set { if (SetProperty(ref _width, value)) { UpdateAspect(true); NotifyOutput(); } } }
    public string HeightText { get => _height; set { if (SetProperty(ref _height, value)) { UpdateAspect(false); NotifyOutput(); } } }
    public string QualityText { get => _quality; set => SetProperty(ref _quality, value); }
    public bool KeepAspect { get => _keepAspect; set { if (SetProperty(ref _keepAspect, value)) { UpdateAspect(true); NotifyOutput(); } } }
    public bool NoUpscale { get => _noUpscale; set { SetProperty(ref _noUpscale, value); NotifyOutput(); } }
    public bool Resize { get => _resize; set { SetProperty(ref _resize, value); NotifyOutput(); } }
    public string OutputDirectory { get => _outputDirectory; set { SetProperty(ref _outputDirectory, value); NotifyOutput(); } }
    public int FormatIndex { get => _formatIndex; set { if (value is < 0 or > 2) return; SetProperty(ref _formatIndex, value); OnPropertyChanged(nameof(IsJpeg)); NotifyOutput(); } }
    public bool IsJpeg => FormatIndex == 1;
    public BitmapSource? Preview { get => _preview; private set => SetProperty(ref _preview, value); }
    public string InfoText => _info?.Summary ?? "选择一张图片查看信息与缩略图。";
    public string ProgressText => $"Total {Items.Count} · Completed {Items.Count(x => x.Status == ImageItemStatus.Succeeded)} · Failed {Items.Count(x => x.Status == ImageItemStatus.Failed)} · Skipped {Items.Count(x => x.Status is ImageItemStatus.Skipped or ImageItemStatus.Conflict)} · Cancelled {Items.Count(x => x.Status == ImageItemStatus.Cancelled)} · Processing {Items.Count(x => x.Status == ImageItemStatus.Processing)}";
    public string OutputPreview
    {
        get
        {
            if (_info is null) return "";
            try
            {
                ImageOptions options = Options();
                var size = ImageToolsService.CalculateSize(_info.VisualWidth, _info.VisualHeight, options);
                return $"预计输出 {size.Width} × {size.Height} px\n{ImageToolsService.OutputPath(_info.Path, options)}";
            }
            catch (Exception ex) when (ex is ArgumentException or FormatException or OverflowException) { return "请检查目标尺寸、质量和输出目录。"; }
        }
    }
    public ImageResult? SelectedItem
    {
        get => _selectedItem;
        set { if (SetProperty(ref _selectedItem, value)) PreviewTask = LoadSelectionAsync(value, ++_previewGeneration); }
    }

    public void AddFiles(IEnumerable<string> paths)
    {
        if (IsBusy || _disposed) return;
        int rejected = 0;
        foreach (string path in paths)
        {
            if (Items.Count >= ImageToolsService.MaximumBatch) { Status = "最多添加 500 张图片。"; break; }
            try
            {
                string fullPath = Path.GetFullPath(path);
                if (Items.Any(item => string.Equals(item.Source, fullPath, StringComparison.OrdinalIgnoreCase))) continue;
                if (Directory.Exists(fullPath) || !File.Exists(fullPath) || !new[] { ".png", ".jpg", ".jpeg", ".bmp" }.Contains(Path.GetExtension(fullPath), StringComparer.OrdinalIgnoreCase))
                { rejected++; continue; }
                Items.Add(new(fullPath, null, ImageItemStatus.Pending));
            }
            catch (Exception ex) when (ex is ArgumentException or IOException or NotSupportedException) { rejected++; }
        }
        if (rejected > 0) Status = "部分项目未加入；请选择 PNG、JPEG 或 BMP 文件，不支持文件夹扫描。";
        SelectedItem ??= Items.FirstOrDefault();
        OnPropertyChanged(nameof(ProgressText)); RunCommand.NotifyCanExecuteChanged();
    }

    private async Task LoadSelectionAsync(ImageResult? item, int generation)
    {
        Preview = null; _info = null; OnPropertyChanged(nameof(InfoText)); NotifyOutput();
        if (item is null || _disposed) return;
        await _previewGate.WaitAsync();
        try
        {
            if (generation != _previewGeneration || _disposed) return;
            var loaded = await ImageToolsService.OnWorkerAsync(() => (Info: ImageToolsService.ReadInfo(item.Source), Preview: ImageToolsService.LoadPreview(item.Source)));
            if (generation != _previewGeneration || _disposed) return;
            _info = loaded.Info; Preview = loaded.Preview;
            UpdateAspect(true); OnPropertyChanged(nameof(InfoText)); NotifyOutput();
        }
        catch (Exception ex) when (ex is IOException or InvalidDataException or ArgumentException or NotSupportedException or UnauthorizedAccessException or InvalidOperationException or System.Runtime.InteropServices.COMException or OutOfMemoryException)
        { if (generation == _previewGeneration && !_disposed) Status = "无法预览图片，请检查格式、文件大小与像素限制。"; }
        finally { _previewGate.Release(); }
    }

    private void UpdateAspect(bool fromWidth)
    {
        if (_aspectUpdating || !KeepAspect || _info is null) return;
        if (!int.TryParse(fromWidth ? WidthText : HeightText, out int value) || value <= 0 || value > ImageToolsService.MaximumDimension) return;
        double computed = fromWidth ? value * (double)_info.VisualHeight / _info.VisualWidth : value * (double)_info.VisualWidth / _info.VisualHeight;
        _aspectUpdating = true;
        try
        {
            string result = Math.Max(1, Math.Round(computed, MidpointRounding.AwayFromZero)).ToString(CultureInfo.InvariantCulture);
            if (fromWidth) HeightText = result; else WidthText = result;
        }
        finally { _aspectUpdating = false; }
    }

    private ImageOptions Options()
    {
        int width = Resize ? int.Parse(WidthText, CultureInfo.InvariantCulture) : 1;
        int height = Resize ? int.Parse(HeightText, CultureInfo.InvariantCulture) : 1;
        int quality = IsJpeg ? int.Parse(QualityText, CultureInfo.InvariantCulture) : 90;
        ImageToolsService.ValidateSize(width, height);
        if (quality is < 1 or > 100) throw new ArgumentOutOfRangeException(nameof(QualityText));
        return new(width, height, KeepAspect, NoUpscale, Resize, (ImageOutputFormat)FormatIndex, quality, OutputDirectory);
    }

    public async Task RunAsync()
    {
        if (IsBusy || _disposed || Items.Count == 0) return;
        ImageOptions options;
        try { options = Options(); }
        catch (Exception ex) when (ex is ArgumentException or FormatException or OverflowException) { Status = "参数无效：尺寸 1–32768 px，总像素最多 4000 万；JPEG 质量 1–100。"; return; }
        IsBusy = true;
        _batchCancellation = new();
        try
        {
            string[] paths = Items.Select(item => item.Source).ToArray();
            var progress = new Progress<ImageResult>(result => { if (IsBusy && !_disposed) ApplyResult(result); });
            var results = await ImageToolsService.ProcessBatchAsync(paths, options, progress, _batchCancellation.Token);
            if (!_disposed)
            {
                foreach (ImageResult result in results) ApplyResult(result);
                Status = _batchCancellation.IsCancellationRequested ? "已取消，未开始的项目不再处理。" : "批处理完成；已存在目标均跳过。";
            }
        }
        catch (Exception) { if (!_disposed) Status = "批处理未完成，请检查文件和输出目录。"; }
        finally { _batchCancellation.Dispose(); _batchCancellation = null; IsBusy = false; }
    }

    private void ApplyResult(ImageResult result)
    {
        for (int i = 0; i < Items.Count; i++) if (Items[i].Source == result.Source) { Items[i] = result; break; }
        OnPropertyChanged(nameof(ProgressText));
    }
    private void NotifyOutput() => OnPropertyChanged(nameof(OutputPreview));
    public void Clear()
    {
        if (IsBusy) return;
        Items.Clear(); SelectedItem = null; Preview = null; _info = null; ++_previewGeneration;
        OnPropertyChanged(nameof(InfoText)); OnPropertyChanged(nameof(ProgressText)); NotifyOutput(); RunCommand.NotifyCanExecuteChanged();
    }
    public void Dispose()
    {
        _disposed = true; _batchCancellation?.Cancel(); ++_previewGeneration;
        Items.Clear(); _selectedItem = null; _info = null; Preview = null;
    }
}
