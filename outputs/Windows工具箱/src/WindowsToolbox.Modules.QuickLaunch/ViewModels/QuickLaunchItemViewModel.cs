using System.ComponentModel;
using System.Windows.Media;
using WindowsToolbox.Core.Utilities;
using WindowsToolbox.Modules.QuickLaunch.Models;

namespace WindowsToolbox.Modules.QuickLaunch.ViewModels;

public sealed class QuickLaunchItemViewModel(QuickLaunchItem model) : ObservableObject
{
    public QuickLaunchItem Model { get; } = model;
    public string Id => Model.Id;
    public string Name => Model.Name;
    public string Target => Model.Target;
    public QuickLaunchItemType Type => Model.Type;
    public string TypeText => QuickLaunchItemRules.TypeText(Model.Type);
    public string Group => string.IsNullOrWhiteSpace(Model.Group) ? "未分组" : Model.Group;
    public bool IsPinned => Model.IsPinned;
    public bool IsMissing => !QuickLaunchItemRules.TargetExists(Model);
    public string StatusText => IsMissing ? "目标不存在" : "可用";
    public string FallbackGlyph => QuickLaunchItemRules.FallbackGlyph(Model.Type);
    public int LaunchCount => Model.LaunchCount;
    public string LastUsedText => Model.LastUsedAt is null ? "尚未使用" : Model.LastUsedAt.Value.ToLocalTime().ToString("MM-dd HH:mm");
    public ImageSource? Icon { get; set; }

    public void Refresh()
    {
        OnPropertyChanged(string.Empty);
    }
}
