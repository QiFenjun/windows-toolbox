using WindowsToolbox.Core.Interfaces;
using WindowsToolbox.Core.Utilities;
using WindowsToolbox.App.Services;

namespace WindowsToolbox.App.ViewModels;

public sealed class ModuleItemViewModel(IToolModule module, LocalizationService localization) : ObservableObject
{
    private bool _isSelected;

    public string Id => module.Id;
    public string DisplayName => localization.IsEnglish ? module.EnglishName : module.DisplayName;
    public string Description => localization.GetModuleDescription(module);
    public string IconKey => module.IconKey;
    public bool IsAvailable => module.IsAvailable;
    public IReadOnlyList<string> Keywords => module.Keywords;

    public bool IsSelected
    {
        get => _isSelected;
        set => SetProperty(ref _isSelected, value);
    }

    public void RefreshLocalization()
    {
        OnPropertyChanged(nameof(DisplayName));
        OnPropertyChanged(nameof(Description));
    }
}
