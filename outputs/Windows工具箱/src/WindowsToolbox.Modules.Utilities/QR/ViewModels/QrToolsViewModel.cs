using System.IO;
using System.Windows.Media.Imaging;
using WindowsToolbox.Core.Commands;
using WindowsToolbox.Core.Utilities;
using WindowsToolbox.Modules.Utilities.QR.Models;
using WindowsToolbox.Modules.Utilities.QR.Services;
using WindowsToolbox.Modules.Utilities.Services;

namespace WindowsToolbox.Modules.Utilities.QR.ViewModels;

public sealed class QrToolsViewModel : ObservableObject, IDisposable
{
    public static IReadOnlyList<int> Sizes { get; } = Array.AsReadOnly(new[] { 256, 384, 512, 768, 1024 });
    public static IReadOnlyList<QrErrorCorrectionOption> ErrorCorrectionOptions { get; } = Array.AsReadOnly<QrErrorCorrectionOption>(
    [
        new(QrErrorCorrection.Low, "L - 低"),
        new(QrErrorCorrection.Medium, "M - 中"),
        new(QrErrorCorrection.Quartile, "Q - 较高"),
        new(QrErrorCorrection.High, "H - 高")
    ]);

    private readonly IQrCodeService _qrCodeService;
    private readonly IImageClipboardAdapter _imageClipboard;
    private readonly IUtilitiesTextClipboardAdapter _textClipboard;
    private CancellationTokenSource? _generationCancellation;
    private CancellationTokenSource? _decodeCancellation;
    private int _generationId;
    private int _decodeId;
    private string _inputText = string.Empty;
    private int _selectedSize = 512;
    private QrErrorCorrectionOption _selectedErrorCorrection = ErrorCorrectionOptions[1];
    private QrQuietZoneStyle _quietZoneStyle = QrQuietZoneStyle.Standard;
    private BitmapSource? _previewImage;
    private BitmapSource? _decodedImage;
    private QrDecodeResult? _decodedResult;
    private bool _isGenerateMode = true;
    private bool _isDecoding;
    private string _status = "输入内容以生成二维码";
    private string _error = string.Empty;
    private bool _disposed;

    public QrToolsViewModel(
        IQrCodeService qrCodeService,
        IImageClipboardAdapter imageClipboard,
        IUtilitiesTextClipboardAdapter textClipboard)
    {
        _qrCodeService = qrCodeService ?? throw new ArgumentNullException(nameof(qrCodeService));
        _imageClipboard = imageClipboard ?? throw new ArgumentNullException(nameof(imageClipboard));
        _textClipboard = textClipboard ?? throw new ArgumentNullException(nameof(textClipboard));
        ShowGenerateModeCommand = new RelayCommand(() => IsGenerateMode = true);
        ShowDecodeModeCommand = new RelayCommand(() => IsGenerateMode = false);
        CopyQrImageCommand = new RelayCommand(CopyQrImage, () => PreviewImage is not null);
        CopyDecodedTextCommand = new RelayCommand(CopyDecodedText, () => DecodedResult is not null);
        UseClipboardImageCommand = new RelayCommand(DecodeClipboardImage);
        CancelDecodeCommand = new RelayCommand(CancelDecode, () => IsDecoding);
    }

    public QrToolsViewModel()
        : this(new QrCodeService(), new WindowsImageClipboardAdapter(), new WindowsUtilitiesTextClipboardAdapter()) { }

    public string InputText
    {
        get => _inputText;
        set
        {
            if (SetProperty(ref _inputText, value ?? string.Empty))
                ScheduleGeneration();
        }
    }

    public int SelectedSize
    {
        get => _selectedSize;
        set
        {
            if (!Sizes.Contains(value))
                throw new ArgumentOutOfRangeException(nameof(value));
            if (SetProperty(ref _selectedSize, value))
                ScheduleGeneration();
        }
    }

    public QrErrorCorrectionOption SelectedErrorCorrection
    {
        get => _selectedErrorCorrection;
        set
        {
            ArgumentNullException.ThrowIfNull(value);
            if (!ErrorCorrectionOptions.Contains(value))
                throw new ArgumentOutOfRangeException(nameof(value));
            if (SetProperty(ref _selectedErrorCorrection, value))
                ScheduleGeneration();
        }
    }

    public QrQuietZoneStyle QuietZoneStyle
    {
        get => _quietZoneStyle;
        set
        {
            if (!Enum.IsDefined(value))
                throw new ArgumentOutOfRangeException(nameof(value));
            if (SetProperty(ref _quietZoneStyle, value))
                ScheduleGeneration();
        }
    }

    public BitmapSource? PreviewImage
    {
        get => _previewImage;
        private set
        {
            if (SetProperty(ref _previewImage, value))
                CopyQrImageCommand.NotifyCanExecuteChanged();
        }
    }

    public BitmapSource? DecodedImage { get => _decodedImage; private set => SetProperty(ref _decodedImage, value); }

    public QrDecodeResult? DecodedResult
    {
        get => _decodedResult;
        private set
        {
            if (SetProperty(ref _decodedResult, value))
                CopyDecodedTextCommand.NotifyCanExecuteChanged();
        }
    }

    public bool IsGenerateMode { get => _isGenerateMode; set => SetProperty(ref _isGenerateMode, value); }
    public bool IsDecoding
    {
        get => _isDecoding;
        private set
        {
            if (SetProperty(ref _isDecoding, value))
                CancelDecodeCommand.NotifyCanExecuteChanged();
        }
    }
    public string Status { get => _status; private set => SetProperty(ref _status, value); }
    public string Error { get => _error; private set => SetProperty(ref _error, value); }

    public RelayCommand ShowGenerateModeCommand { get; }
    public RelayCommand ShowDecodeModeCommand { get; }
    public RelayCommand CopyQrImageCommand { get; }
    public RelayCommand CopyDecodedTextCommand { get; }
    public RelayCommand UseClipboardImageCommand { get; }
    public RelayCommand CancelDecodeCommand { get; }
    public IReadOnlyList<int> AvailableSizes => Sizes;
    public IReadOnlyList<QrErrorCorrectionOption> AvailableErrorCorrections => ErrorCorrectionOptions;

    public void SavePng(Stream output)
    {
        if (PreviewImage is null)
            throw new InvalidOperationException("当前没有可保存的二维码。");
        _qrCodeService.SavePng(PreviewImage, output);
    }

    public async Task DecodeFileAsync(string path)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(path);
        CancellationTokenSource current = StartDecode();
        int id = _decodeId;
        try
        {
            BitmapSource image = await Task.Run(() => QrImageFileLoader.Load(path), current.Token).ConfigureAwait(true);
            await DecodeImageCoreAsync(image, current, id).ConfigureAwait(true);
        }
        catch (OperationCanceledException) { }
        catch
        {
            if (IsCurrentDecode(current, id))
                SetDecodeFailure("无法读取该图片。");
        }
        finally
        {
            FinishDecode(current, id);
        }
    }

    public Task DecodeDroppedFilesAsync(IReadOnlyList<string> paths)
    {
        ArgumentNullException.ThrowIfNull(paths);
        if (paths.Count != 1)
        {
            Error = "一次请选择一张图片。";
            Status = "图片数量不正确";
            return Task.CompletedTask;
        }
        if (Directory.Exists(paths[0]))
        {
            Error = "请拖入单张图片，不支持文件夹。";
            Status = "输入不是图片文件";
            return Task.CompletedTask;
        }
        return DecodeFileAsync(paths[0]);
    }

    public async Task DecodeImageAsync(BitmapSource image)
    {
        ArgumentNullException.ThrowIfNull(image);
        CancellationTokenSource current = StartDecode();
        int id = _decodeId;
        try { await DecodeImageCoreAsync(image, current, id).ConfigureAwait(true); }
        finally { FinishDecode(current, id); }
    }

    public void Dispose()
    {
        if (_disposed)
            return;
        _disposed = true;
        CancelAndDispose(ref _generationCancellation);
        CancelAndDispose(ref _decodeCancellation);
    }

    private void ScheduleGeneration()
    {
        if (_disposed)
            return;
        CancellationTokenSource current = new();
        CancellationTokenSource? previous = Interlocked.Exchange(ref _generationCancellation, current);
        previous?.Cancel();
        int id = Interlocked.Increment(ref _generationId);
        Error = string.Empty;
        if (string.IsNullOrWhiteSpace(InputText))
        {
            PreviewImage = null;
            Status = "输入内容以生成二维码";
            Interlocked.CompareExchange(ref _generationCancellation, null, current);
            current.Dispose();
            return;
        }

        Status = "正在更新预览…";
        _ = GenerateAsync(id, current, InputText, SelectedSize, SelectedErrorCorrection.Value, QuietZoneStyle);
    }

    private async Task GenerateAsync(
        int id,
        CancellationTokenSource current,
        string text,
        int size,
        QrErrorCorrection correction,
        QrQuietZoneStyle quietZone)
    {
        try
        {
            await Task.Delay(250, current.Token).ConfigureAwait(true);
            BitmapSource image = await Task.Run(
                () => _qrCodeService.Generate(text, size, correction, quietZone), current.Token).ConfigureAwait(true);
            if (current.IsCancellationRequested || id != _generationId || _disposed)
                return;
            PreviewImage = image;
            Error = string.Empty;
            Status = $"预览已更新 · {size} × {size}";
        }
        catch (OperationCanceledException) { }
        catch
        {
            if (id == _generationId && !_disposed)
            {
                Error = "无法生成二维码。请缩短内容或降低纠错级别后重试。";
                Status = "生成失败，保留上一次预览";
            }
        }
        finally
        {
            Interlocked.CompareExchange(ref _generationCancellation, null, current);
            current.Dispose();
        }
    }

    private void CopyQrImage()
    {
        if (PreviewImage is null)
            return;
        try
        {
            _imageClipboard.SetImage(PreviewImage);
            Error = string.Empty;
            Status = "已复制二维码图片";
        }
        catch
        {
            Error = "无法写入剪贴板，请稍后重试。";
        }
    }

    private void CopyDecodedText()
    {
        if (DecodedResult is null)
            return;
        try
        {
            _textClipboard.SetText(DecodedResult.Text);
            Error = string.Empty;
            Status = "已复制识别内容";
        }
        catch
        {
            Error = "无法写入剪贴板，请稍后重试。";
        }
    }

    private void DecodeClipboardImage()
    {
        try
        {
            BitmapSource? image = _imageClipboard.GetImage();
            if (image is null)
            {
                Error = string.Empty;
                Status = "剪贴板中没有图片";
                return;
            }
            _ = DecodeImageAsync(image);
        }
        catch
        {
            Error = "无法读取剪贴板图片。";
        }
    }

    private CancellationTokenSource StartDecode()
    {
        CancellationTokenSource current = new();
        CancellationTokenSource? previous = Interlocked.Exchange(ref _decodeCancellation, current);
        previous?.Cancel();
        int id = Interlocked.Increment(ref _decodeId);
        IsDecoding = true;
        DecodedResult = null;
        DecodedImage = null;
        Error = string.Empty;
        Status = "正在识别二维码…";
        return current;
    }

    private async Task DecodeImageCoreAsync(BitmapSource image, CancellationTokenSource current, int id)
    {
        try
        {
            if (!image.IsFrozen)
                image = image.CloneCurrentValue();
            if (image.CanFreeze && !image.IsFrozen)
                image.Freeze();
            QrDecodeResult? result = await Task.Run(
                () => _qrCodeService.Decode(image, current.Token), current.Token).ConfigureAwait(true);
            if (!IsCurrentDecode(current, id))
                return;
            DecodedImage = image;
            DecodedResult = result;
            Error = string.Empty;
            Status = result is null ? "未识别到二维码" : "识别完成";
        }
        catch (OperationCanceledException) { }
        catch
        {
            if (IsCurrentDecode(current, id))
                SetDecodeFailure("无法读取该图片。");
        }
    }

    private void SetDecodeFailure(string message)
    {
        DecodedResult = null;
        DecodedImage = null;
        Error = message;
        Status = "识别失败";
    }

    private bool IsCurrentDecode(CancellationTokenSource current, int id) =>
        !_disposed && !current.IsCancellationRequested && id == _decodeId;

    private void FinishDecode(CancellationTokenSource current, int id)
    {
        if (Interlocked.CompareExchange(ref _decodeCancellation, null, current) == current && id == _decodeId)
            IsDecoding = false;
        current.Dispose();
    }

    private void CancelDecode()
    {
        Interlocked.Increment(ref _decodeId);
        CancellationTokenSource? current = Interlocked.Exchange(ref _decodeCancellation, null);
        current?.Cancel();
        IsDecoding = false;
        Status = "已取消二维码识别";
    }

    private static void CancelAndDispose(ref CancellationTokenSource? source)
    {
        CancellationTokenSource? current = Interlocked.Exchange(ref source, null);
        current?.Cancel();
        current?.Dispose();
    }

}
