using System.Collections.ObjectModel;
using WindowsToolbox.App.Services;
using WindowsToolbox.Core.Commands;
using WindowsToolbox.Core.Interfaces;
using WindowsToolbox.Core.Utilities;

namespace WindowsToolbox.App.ViewModels;

public sealed class HomeViewModel : ObservableObject
{
    private readonly IModuleRegistry _moduleRegistry;
    private readonly ISettingsService _settingsService;
    private readonly Action<string> _navigate;
    private readonly LocalizationService _localization;

    public HomeViewModel(
        IModuleRegistry moduleRegistry,
        ISettingsService settingsService,
        Action<string> navigate,
        IMotionService motionService,
        LocalizationService localization)
    {
        _moduleRegistry = moduleRegistry;
        _settingsService = settingsService;
        _navigate = navigate;
        MotionService = motionService;
        _localization = localization;
        OpenModuleCommand = new RelayCommand<string>(id =>
        {
            if (!string.IsNullOrWhiteSpace(id))
                _navigate(id);
        });
        RefreshLocalization();
    }

    public ObservableCollection<ModuleItemViewModel> Modules { get; } = [];
    public ObservableCollection<ModuleItemViewModel> RecentModules { get; } = [];
    public IMotionService MotionService { get; }
    public int InstalledModuleCount => Modules.Count;
    public bool HasRecentModules => RecentModules.Count > 0;
    public RelayCommand<string> OpenModuleCommand { get; }

    public void Refresh()
    {
        RefreshRecentModules();
    }

    public void RefreshLocalization()
    {
        Modules.Clear();
        foreach (IToolModule module in _moduleRegistry.Modules.Where(module => module.IsAvailable))
            Modules.Add(new ModuleItemViewModel(module, _localization));

        RefreshRecentModules();
    }

    private void RefreshRecentModules()
    {
        RecentModules.Clear();
        foreach (string id in _settingsService.Settings.RecentModuleIds)
        {
            IToolModule? module = _moduleRegistry.Find(id);
            if (module?.IsAvailable == true)
                RecentModules.Add(new ModuleItemViewModel(module, _localization));
        }

        OnPropertyChanged(nameof(InstalledModuleCount));
        OnPropertyChanged(nameof(HasRecentModules));
    }
}
