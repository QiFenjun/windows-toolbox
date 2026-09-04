using System.Windows;
using WindowsToolbox.App.Services;
using WindowsToolbox.App.ViewModels;
using WindowsToolbox.Core.Interfaces;
using WindowsToolbox.Core.Services;
using WindowsToolbox.Modules.InstalledApps;
using WindowsToolbox.Modules.NetworkTraffic;
using WindowsToolbox.Modules.NetworkTraffic.Services;
using WindowsToolbox.Modules.NetworkTraffic.ViewModels;
using WindowsToolbox.Modules.Shutdown;
using WindowsToolbox.Modules.Shutdown.Services;
using Forms = System.Windows.Forms;

namespace WindowsToolbox.App;

public partial class App : System.Windows.Application
{
    private Forms.NotifyIcon? _trayIcon;
    private bool _isExplicitExit;

    protected override async void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);

        if (e.Args.Length == 2 && string.Equals(e.Args[0], "--network-monitor-helper", StringComparison.Ordinal))
        {
            Shutdown(await NetworkMonitorHelperHost.RunAsync(e.Args[1]));
            return;
        }

        ISettingsService settingsService = new SettingsService();
        await settingsService.LoadAsync();

        IThemeService themeService = new ThemeService();
        themeService.Apply(settingsService.Settings.Theme);

        bool backgroundStartup = e.Args.Any(argument =>
            string.Equals(argument, "--background-network-monitor", StringComparison.Ordinal));
        IModuleRegistry moduleRegistry = new ModuleRegistry();
        INavigationService navigationService = new NavigationService();
        IShutdownService shutdownService = new ShutdownService();

        moduleRegistry.Register(new ShutdownModule(shutdownService, settingsService));
        moduleRegistry.Register(new InstalledAppsModule());
        NetworkTrafficModule networkTrafficModule = new(settingsService);
        moduleRegistry.Register(networkTrafficModule);
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
        window.Closing += (_, closingEventArgs) =>
        {
            if (_isExplicitExit || !settingsService.Settings.NetworkTrafficContinueInBackground ||
                networkTrafficModule.CurrentViewModel?.IsMonitoring != true)
                return;

            closingEventArgs.Cancel = true;
            window.Hide();
            ShowTrayIcon(window, networkTrafficModule);
        };
        window.Show();
        mainViewModel.Start();

        if (backgroundStartup)
        {
            window.Hide();
            ShowTrayIcon(window, networkTrafficModule);
            NetworkTrafficViewModel viewModel = (NetworkTrafficViewModel)networkTrafficModule.CreateViewModel();
            _ = viewModel.StartAsyncForBackgroundAsync();
        }
    }

    private void ShowTrayIcon(MainWindow window, NetworkTrafficModule networkTrafficModule)
    {
        if (_trayIcon is not null)
            return;

        Forms.ContextMenuStrip menu = new();
        menu.Items.Add("显示 Windows工具箱", null, (_, _) =>
        {
            window.Show();
            window.Activate();
        });
        menu.Items.Add("退出 Windows工具箱", null, async (_, _) =>
        {
            _isExplicitExit = true;
            if (networkTrafficModule.CurrentViewModel is not null)
                await networkTrafficModule.CurrentViewModel.StopAsyncForExitAsync();
            _trayIcon?.Dispose();
            _trayIcon = null;
            window.Close();
        });
        _trayIcon = new Forms.NotifyIcon
        {
            Icon = System.Drawing.SystemIcons.Application,
            Text = "Windows 工具箱正在后台监控网络流量",
            ContextMenuStrip = menu,
            Visible = true
        };
        _trayIcon.DoubleClick += (_, _) =>
        {
            window.Show();
            window.Activate();
        };
    }
}
