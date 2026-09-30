using WindowsToolbox.Core.Utilities;
using WindowsToolbox.Core.Commands;
using WindowsToolbox.Modules.Utilities.Models;
using WindowsToolbox.Modules.Utilities.QR.Services;
using WindowsToolbox.Modules.Utilities.QR.ViewModels;
using WindowsToolbox.Modules.Utilities.Color.ViewModels;
using WindowsToolbox.Modules.Utilities.Services;
using WindowsToolbox.Modules.Utilities.Time.ViewModels;
using WindowsToolbox.Modules.Utilities.Random.ViewModels;
using WindowsToolbox.Modules.Utilities.Unit.ViewModels;
using WindowsToolbox.Modules.Utilities.Developer.ViewModels;
using WindowsToolbox.Modules.Utilities.Image.ViewModels;
using WindowsToolbox.Modules.Utilities.Regex.ViewModels;

namespace WindowsToolbox.Modules.Utilities.ViewModels;

public sealed class UtilitiesViewModel : ObservableObject, IDisposable
{
    public static IReadOnlyList<UtilityToolDescriptor> Tools { get; } = Array.AsReadOnly<UtilityToolDescriptor>(
    [
        new("qr", "二维码工具", "QR Tools", "QRCode", "离线生成与识别 QR Code"),
        new("color", "颜色工具", "Color Tools", "Color", "颜色转换与屏幕取色"),
        new("time-tools", "时间工具", "Time Tools", "Clock", "时间戳、时区与日期时间快速转换"),
        new("random-tools", "随机工具", "Random Tools", "Shuffle", "生成 UUID、安全随机字符串和随机数字"),
        new("unit-converter", "单位转换", "Unit Converter", "Scale", "长度、质量、温度、压力等常用单位快速转换"),
        new("developer-tools", "开发者工具", "Developer Tools", "Code", "进制转换、文本哈希与 UUID 检查"),
        new("image-tools", "图片工具", "Image Tools", "Image", "图片尺寸调整、格式转换与信息查看"),
        new("regex-tools", "正则工具", "Regex Tools", "Code", "正则表达式匹配、分组与替换预览")
    ]);

    private string _selectedToolId = "qr";

    public UtilitiesViewModel()
        : this(new QrToolsViewModel(new QrCodeService(), new WindowsImageClipboardAdapter(), new WindowsUtilitiesTextClipboardAdapter()),
            new ColorToolsViewModel(), new TimeToolsViewModel(), new RandomToolsViewModel()) { }

    public UtilitiesViewModel(QrToolsViewModel qrTools)
        : this(qrTools, new ColorToolsViewModel(), new TimeToolsViewModel()) { }

    public UtilitiesViewModel(QrToolsViewModel qrTools, ColorToolsViewModel colorTools)
        : this(qrTools, colorTools, new TimeToolsViewModel(), new RandomToolsViewModel()) { }

    public UtilitiesViewModel(QrToolsViewModel qrTools, ColorToolsViewModel colorTools, TimeToolsViewModel timeTools)
        : this(qrTools, colorTools, timeTools, new RandomToolsViewModel()) { }

    public UtilitiesViewModel(QrToolsViewModel qrTools, ColorToolsViewModel colorTools, TimeToolsViewModel timeTools, RandomToolsViewModel randomTools)
    {
        QrTools = qrTools ?? throw new ArgumentNullException(nameof(qrTools));
        ColorTools = colorTools ?? throw new ArgumentNullException(nameof(colorTools));
        TimeTools = timeTools ?? throw new ArgumentNullException(nameof(timeTools));
        RandomTools = randomTools ?? throw new ArgumentNullException(nameof(randomTools));
        UnitConverter = new UnitConverterViewModel();
        DeveloperTools = new DeveloperToolsViewModel();
        SelectToolCommand = new RelayCommand<string>(id => SelectedToolId = id!, id =>
            id is not null && Tools.Any(tool => tool.Id == id));
    }

    public QrToolsViewModel QrTools { get; }
    public ColorToolsViewModel ColorTools { get; }
    public TimeToolsViewModel TimeTools { get; }
    public RandomToolsViewModel RandomTools { get; }
    public UnitConverterViewModel UnitConverter { get; }
    public DeveloperToolsViewModel DeveloperTools { get; }
    public ImageToolsViewModel ImageTools { get; } = new();
    public RegexToolsViewModel RegexTools { get; } = new();
    public RelayCommand<string> SelectToolCommand { get; }

    public UtilityToolDescriptor SelectedTool => Tools.First(tool => tool.Id == SelectedToolId);
    public object SelectedToolContent => SelectedToolId switch
    {
        "qr" => QrTools,
        "color" => ColorTools,
        "time-tools" => TimeTools,
        "random-tools" => RandomTools,
        "unit-converter" => UnitConverter,
        "developer-tools" => DeveloperTools,
        "image-tools" => ImageTools,
        "regex-tools" => RegexTools,
        _ => SelectedTool
    };

    public string SelectedToolId
    {
        get => _selectedToolId;
        set
        {
            if (Tools.All(tool => !string.Equals(tool.Id, value, StringComparison.Ordinal)))
                throw new ArgumentOutOfRangeException(nameof(value));
            if (SetProperty(ref _selectedToolId, value))
            {
                if (value != "color")
                    ColorTools.CancelPicker();
                OnPropertyChanged(nameof(SelectedTool));
                OnPropertyChanged(nameof(SelectedToolContent));
            }
        }
    }

    public void Dispose()
    {
        QrTools.Dispose();
        ColorTools.Dispose();
        TimeTools.Dispose();
        RandomTools.Dispose();
        ImageTools.Dispose();
        RegexTools.Dispose();
    }
}
