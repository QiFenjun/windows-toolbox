using System.Collections.ObjectModel;
using WindowsToolbox.Core.Commands;
using WindowsToolbox.Core.Interfaces;
using WindowsToolbox.Core.Utilities;

namespace WindowsToolbox.App.ViewModels;

public sealed class HomeViewModel : ObservableObject
{
    private readonly IModuleRegistry _moduleRegistry;
    private readonly ISettingsService _settingsService;
    private readonly Action<string> _navigate;

    public HomeViewModel(
        IModuleRegistry moduleRegistry,
        ISettingsService settingsService,
        Action<string> navigate)
    {
        _moduleRegistry = moduleRegistry;
        _settingsService = settingsService;
        _navigate = navigate;
        OpenModuleCommand = new RelayCommand<string>(id =>
        {
            if (!string.IsNullOrWhiteSpace(id))
                _navigate(id);
        });
        Refresh();
    }

    public ObservableCollection<ModuleItemViewModel> Modules { get; } = [];
    public ObservableCollection<ModuleItemViewModel> RecentModules { get; } = [];
    public int InstalledModuleCount => Modules.Count;
    public bool HasRecentModules => RecentModules.Count > 0;
    public RelayCommand<string> OpenModuleCommand { get; }

    public void Refresh()
    {
        Modules.Clear();
        foreach (IToolModule module in _moduleRegistry.Modules.Where(module => module.IsAvailable))
            Modules.Add(new ModuleItemViewModel(module));

        RecentModules.Clear();
        foreach (string id in _settingsService.Settings.RecentModuleIds)
        {
            IToolModule? module = _moduleRegistry.Find(id);
            if (module?.IsAvailable == true)
                RecentModules.Add(new ModuleItemViewModel(module));
        }

        OnPropertyChanged(nameof(InstalledModuleCount));
        OnPropertyChanged(nameof(HasRecentModules));
    }
}
