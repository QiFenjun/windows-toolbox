using System.Windows;
using WindowsToolbox.App.Services;
using WindowsToolbox.App.ViewModels;
using WindowsToolbox.Core.Interfaces;
using WindowsToolbox.Core.Services;
using WindowsToolbox.Modules.Shutdown;
using WindowsToolbox.Modules.Shutdown.Services;

namespace WindowsToolbox.App;

public partial class App : Application
{
    protected override async void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);

        ISettingsService settingsService = new SettingsService();
        await settingsService.LoadAsync();

        IThemeService themeService = new ThemeService();
        themeService.Apply(settingsService.Settings.Theme);

        IModuleRegistry moduleRegistry = new ModuleRegistry();
        INavigationService navigationService = new NavigationService();
        IShutdownService shutdownService = new ShutdownService();

        moduleRegistry.Register(new ShutdownModule(shutdownService, settingsService));
        foreach (IToolModule module in moduleRegistry.Modules)
        {
            if (string.IsNullOrWhiteSpace(module.ResourceDictionaryPath))
                continue;

            Resources.MergedDictionaries.Add(new ResourceDictionary
            {
                Source = new Uri(module.ResourceDictionaryPath, UriKind.RelativeOrAbsolute)
            });
        }

        MainWindowViewModel mainViewModel = new(
            moduleRegistry,
            navigationService,
            settingsService,
            themeService);

        MainWindow window = new()
        {
            DataContext = mainViewModel
        };

        MainWindow = window;
        window.Show();
        mainViewModel.Start();
    }
}
