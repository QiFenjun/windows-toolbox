using WindowsToolbox.Core.Interfaces;
using WindowsToolbox.Core.Utilities;

namespace WindowsToolbox.App.ViewModels;

public sealed class ModuleItemViewModel(IToolModule module) : ObservableObject
{
    private bool _isSelected;

    public string Id => module.Id;
    public string DisplayName => module.DisplayName;
    public string Description => module.Description;
    public string Category => module.Category;
    public string IconKey => module.IconKey;
    public bool IsAvailable => module.IsAvailable;
    public IReadOnlyList<string> Keywords => module.Keywords;

    public bool IsSelected
    {
        get => _isSelected;
        set => SetProperty(ref _isSelected, value);
    }
}
