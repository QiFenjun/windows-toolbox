using System.Collections.ObjectModel;
using System.Globalization;
using WindowsToolbox.Core.Commands;
using WindowsToolbox.Core.Utilities;
using WindowsToolbox.Modules.Utilities.Color.Models;
using WindowsToolbox.Modules.Utilities.Color.Services;
using WindowsToolbox.Modules.Utilities.Services;
using MediaColor = System.Windows.Media.Color;
using MediaColors = System.Windows.Media.Colors;
using SolidColorBrush = System.Windows.Media.SolidColorBrush;

namespace WindowsToolbox.Modules.Utilities.Color.ViewModels;

public sealed class ColorToolsViewModel : ObservableObject, IDisposable
{
    public const int MaximumRecentColors = 10;

    private readonly IUtilitiesTextClipboardAdapter _clipboard;
    private readonly IScreenColorPicker _screenPicker;
    private CancellationTokenSource? _pickerCancellation;
    private ColorValue _color = new(byte.MaxValue, byte.MaxValue, 0, 0);
    private string _hexText = "#FF0000";
    private string _redText = "255";
    private string _greenText = "0";
    private string _blueText = "0";
    private string _hueText = "0";
    private string _saturationText = "100";
    private string _lightnessText = "50";
    private double _alphaPercent = 100;
    private SolidColorBrush _previewBrush = new(MediaColors.Red);
    private bool _isPicking;
    private bool _isUpdatingFields;
    private bool _disposed;
    private string _status = "输入 HEX、RGB 或 HSL 颜色";
    private string _error = string.Empty;

    public ColorToolsViewModel(IUtilitiesTextClipboardAdapter clipboard, IScreenColorPicker screenPicker)
    {
        _clipboard = clipboard ?? throw new ArgumentNullException(nameof(clipboard));
        _screenPicker = screenPicker ?? throw new ArgumentNullException(nameof(screenPicker));
        CopyHexCommand = new RelayCommand(() => Copy(CurrentColor.ToHex()));
        CopyRgbCommand = new RelayCommand(CopyRgb);
        CopyHslCommand = new RelayCommand(CopyHsl);
        StartPickerCommand = new RelayCommand(() => _ = PickAsync(), () => !IsPicking);
        CancelPickerCommand = new RelayCommand(CancelPicker, () => IsPicking);
        SelectRecentCommand = new RelayCommand<RecentColorItem>(item =>
        {
            if (item is not null)
                ApplyColor(item.Value, addRecent: true);
        }, item => item is not null);
    }

    public ColorToolsViewModel()
        : this(new WindowsUtilitiesTextClipboardAdapter(),
            new WindowsScreenColorPicker(new WindowsScreenColorSampler(), () => System.Windows.Application.Current?.MainWindow)) { }

    public ColorValue CurrentColor => _color;
    public MediaColor PreviewColor => _previewBrush.Color;
    public string RgbDisplay => _color.IsOpaque
        ? $"RGB({_color.R}, {_color.G}, {_color.B})"
        : $"RGBA({_color.R}, {_color.G}, {_color.B}, {_color.A})";
    public string HslDisplay => FormatHsl(ColorConversionService.ToHsl(_color), _color.A);
    public SolidColorBrush PreviewBrush { get => _previewBrush; private set => SetProperty(ref _previewBrush, value); }
    public ObservableCollection<RecentColorItem> RecentColors { get; } = [];
    public RelayCommand CopyHexCommand { get; }
    public RelayCommand CopyRgbCommand { get; }
    public RelayCommand CopyHslCommand { get; }
    public RelayCommand StartPickerCommand { get; }
    public RelayCommand CancelPickerCommand { get; }
    public RelayCommand<RecentColorItem> SelectRecentCommand { get; }

    public string HexText
    {
        get => _hexText;
        set
        {
            if (!SetProperty(ref _hexText, value ?? string.Empty) || _isUpdatingFields) return;
            if (!ColorConversionService.TryParseHex(value, out ColorValue parsed))
                SetInvalid("HEX 格式无效。支持 #RGB、#RRGGBB 和 #AARRGGBB。");
            else
                ApplyColor(parsed, addRecent: true);
        }
    }

    public string RedText { get => _redText; set { if (SetProperty(ref _redText, value ?? string.Empty) && !_isUpdatingFields) UpdateFromRgb(); } }
    public string GreenText { get => _greenText; set { if (SetProperty(ref _greenText, value ?? string.Empty) && !_isUpdatingFields) UpdateFromRgb(); } }
    public string BlueText { get => _blueText; set { if (SetProperty(ref _blueText, value ?? string.Empty) && !_isUpdatingFields) UpdateFromRgb(); } }
    public string HueText { get => _hueText; set { if (SetProperty(ref _hueText, value ?? string.Empty) && !_isUpdatingFields) UpdateFromHsl(); } }
    public string SaturationText { get => _saturationText; set { if (SetProperty(ref _saturationText, value ?? string.Empty) && !_isUpdatingFields) UpdateFromHsl(); } }
    public string LightnessText { get => _lightnessText; set { if (SetProperty(ref _lightnessText, value ?? string.Empty) && !_isUpdatingFields) UpdateFromHsl(); } }

    public double AlphaPercent
    {
        get => _alphaPercent;
        set
        {
            if (!double.IsFinite(value) || value is < 0d or > 100d)
                throw new ArgumentOutOfRangeException(nameof(value));
            if (SetProperty(ref _alphaPercent, value) && !_isUpdatingFields)
                ApplyColor(_color with { A = (byte)Math.Round(value * 255d / 100d, MidpointRounding.AwayFromZero) }, addRecent: true);
        }
    }

    public string AlphaLabel => $"{Math.Round(_color.A * 100d / 255d):0}% · {_color.A}/255";
    public bool IsPicking
    {
        get => _isPicking;
        private set
        {
            if (SetProperty(ref _isPicking, value))
            {
                StartPickerCommand.NotifyCanExecuteChanged();
                CancelPickerCommand.NotifyCanExecuteChanged();
            }
        }
    }
    public string Status { get => _status; private set => SetProperty(ref _status, value); }
    public string Error { get => _error; private set => SetProperty(ref _error, value); }

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;
        _pickerCancellation?.Cancel();
    }

    public void CancelPicker()
    {
        if (!IsPicking) return;
        _pickerCancellation?.Cancel();
        Status = "取色已取消";
    }

    private void UpdateFromRgb()
    {
        if (!ColorConversionService.TryParseByte(RedText, out byte red) ||
            !ColorConversionService.TryParseByte(GreenText, out byte green) ||
            !ColorConversionService.TryParseByte(BlueText, out byte blue))
        {
            SetInvalid("RGB 每个数值必须为 0 至 255。颜色预览保持不变。");
            return;
        }
        ApplyColor(new ColorValue(_color.A, red, green, blue), addRecent: true);
    }

    private void UpdateFromHsl()
    {
        if (!ColorConversionService.TryParseHue(HueText, out double hue) ||
            !ColorConversionService.TryParsePercent(SaturationText, out double saturation) ||
            !ColorConversionService.TryParsePercent(LightnessText, out double lightness))
        {
            SetInvalid("Hue 必须为 0 至 360，Saturation 和 Lightness 必须为 0 至 100。颜色预览保持不变。");
            return;
        }
        ApplyColor(ColorConversionService.FromHsl(hue, saturation, lightness, _color.A), addRecent: true);
    }

    private void ApplyColor(ColorValue color, bool addRecent)
    {
        _isUpdatingFields = true;
        try
        {
            if (SetProperty(ref _color, color, nameof(CurrentColor)))
            {
                _hexText = color.ToHex();
                _redText = color.R.ToString(CultureInfo.InvariantCulture);
                _greenText = color.G.ToString(CultureInfo.InvariantCulture);
                _blueText = color.B.ToString(CultureInfo.InvariantCulture);
                HslColor hsl = ColorConversionService.ToHsl(color);
                _hueText = FormatNumber(hsl.Hue);
                _saturationText = FormatNumber(hsl.Saturation);
                _lightnessText = FormatNumber(hsl.Lightness);
                _alphaPercent = color.A * 100d / 255d;
                OnPropertyChanged(nameof(HexText));
                OnPropertyChanged(nameof(RedText));
                OnPropertyChanged(nameof(GreenText));
                OnPropertyChanged(nameof(BlueText));
                OnPropertyChanged(nameof(HueText));
                OnPropertyChanged(nameof(SaturationText));
                OnPropertyChanged(nameof(LightnessText));
                OnPropertyChanged(nameof(AlphaPercent));
                OnPropertyChanged(nameof(AlphaLabel));
                OnPropertyChanged(nameof(RgbDisplay));
                OnPropertyChanged(nameof(HslDisplay));
                SetPreviewBrush(color.ToMediaColor());
            }
            Error = string.Empty;
            Status = "颜色已更新";
            if (addRecent)
                AddRecent(color);
        }
        finally { _isUpdatingFields = false; }
    }

    private void SetPreviewBrush(MediaColor color)
    {
        SolidColorBrush brush = new(color);
        brush.Freeze();
        PreviewBrush = brush;
        OnPropertyChanged(nameof(PreviewColor));
    }

    private void AddRecent(ColorValue color)
    {
        for (int index = RecentColors.Count - 1; index >= 0; index--)
            if (RecentColors[index].Value == color)
                RecentColors.RemoveAt(index);
        RecentColors.Insert(0, new RecentColorItem(color));
        while (RecentColors.Count > MaximumRecentColors)
            RecentColors.RemoveAt(RecentColors.Count - 1);
    }

    private async Task PickAsync()
    {
        if (_disposed || IsPicking) return;
        CancellationTokenSource current = new();
        _pickerCancellation = current;
        IsPicking = true;
        Error = string.Empty;
        Status = "移动鼠标预览颜色；单击确认，Esc 或右键取消";
        SetPreviewBrush(_color.ToMediaColor());
        try
        {
            MediaColor? picked = await _screenPicker.PickAsync(color => SetPreviewBrush(color), current.Token).ConfigureAwait(true);
            if (picked is MediaColor color && !current.IsCancellationRequested && !_disposed)
            {
                ApplyColor(ColorValue.FromMediaColor(color), addRecent: true);
                Status = "屏幕取色完成";
            }
            else if (!_disposed)
                Status = "取色已取消";
        }
        catch (OperationCanceledException)
        {
            if (!_disposed) Status = "取色已取消";
        }
        catch
        {
            if (!_disposed)
            {
                Error = "屏幕取色失败。";
                Status = "取色失败";
            }
        }
        finally
        {
            if (ReferenceEquals(_pickerCancellation, current))
                _pickerCancellation = null;
            current.Dispose();
            if (!_disposed)
            {
                IsPicking = false;
                SetPreviewBrush(_color.ToMediaColor());
            }
        }
    }

    private void CopyRgb() => Copy(RgbDisplay);

    private void CopyHsl() => Copy(HslDisplay);

    private static string FormatHsl(HslColor hsl, byte alpha)
    {
        string values = $"{Math.Round(hsl.Hue):0}, {Math.Round(hsl.Saturation):0}%, {Math.Round(hsl.Lightness):0}%";
        return alpha == byte.MaxValue
            ? $"HSL({values})"
            : $"HSLA({values}, {Math.Round(alpha * 100d / 255d):0}%)";
    }

    private void Copy(string text)
    {
        try
        {
            _clipboard.SetText(text);
            Error = string.Empty;
            Status = "已复制颜色格式";
        }
        catch { Error = "无法写入剪贴板，请稍后重试。"; }
    }

    private void SetInvalid(string message)
    {
        Error = message;
        Status = "输入无效，颜色预览保持不变";
    }

    private static string FormatNumber(double value) =>
        value.ToString("0.##", CultureInfo.InvariantCulture);
}
