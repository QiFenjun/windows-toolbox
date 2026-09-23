using WindowsToolbox.Core.Utilities;
using WindowsToolbox.Core.Commands;
using WindowsToolbox.Modules.Utilities.Models;
using WindowsToolbox.Modules.Utilities.QR.Services;
using WindowsToolbox.Modules.Utilities.QR.ViewModels;
using WindowsToolbox.Modules.Utilities.Color.ViewModels;
using WindowsToolbox.Modules.Utilities.Services;

namespace WindowsToolbox.Modules.Utilities.ViewModels;

public sealed class UtilitiesViewModel : ObservableObject, IDisposable
{
    public static IReadOnlyList<UtilityToolDescriptor> Tools { get; } = Array.AsReadOnly<UtilityToolDescriptor>(
    [
        new("qr", "二维码工具", "QR Tools", "QRCode", "离线生成与识别 QR Code"),
        new("color", "颜色工具", "Color Tools", "Color", "颜色转换与屏幕取色")
    ]);

    private string _selectedToolId = "qr";

    public UtilitiesViewModel()
        : this(new QrToolsViewModel(new QrCodeService(), new WindowsImageClipboardAdapter(), new WindowsUtilitiesTextClipboardAdapter()), new ColorToolsViewModel()) { }

    public UtilitiesViewModel(QrToolsViewModel qrTools)
        : this(qrTools, new ColorToolsViewModel()) { }

    public UtilitiesViewModel(QrToolsViewModel qrTools, ColorToolsViewModel colorTools)
    {
        QrTools = qrTools ?? throw new ArgumentNullException(nameof(qrTools));
        ColorTools = colorTools ?? throw new ArgumentNullException(nameof(colorTools));
        SelectToolCommand = new RelayCommand<string>(id => SelectedToolId = id!, id =>
            id is not null && Tools.Any(tool => tool.Id == id));
    }

    public QrToolsViewModel QrTools { get; }
    public ColorToolsViewModel ColorTools { get; }
    public RelayCommand<string> SelectToolCommand { get; }

    public UtilityToolDescriptor SelectedTool => Tools.First(tool => tool.Id == SelectedToolId);
    public object SelectedToolContent => SelectedToolId == "qr" ? QrTools : ColorTools;

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
    }
}
