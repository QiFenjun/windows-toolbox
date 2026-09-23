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
using WindowsToolbox.Modules.ClipboardPlus;
using WindowsToolbox.Modules.TextTools;
using WindowsToolbox.Modules.FileTools;
using WindowsToolbox.Modules.QuickLaunch;
using WindowsToolbox.Modules.WindowTools;
using WindowsToolbox.Modules.LockInspector;
using WindowsToolbox.Modules.KeepAwake;
using WindowsToolbox.Modules.Utilities;
using Forms = System.Windows.Forms;

namespace WindowsToolbox.App;

public partial class App : System.Windows.Application
{
    private Forms.NotifyIcon? _trayIcon;
    private QuickLaunchModule? _quickLaunchModule;
    private LockInspectorModule? _lockInspectorModule;
    private KeepAwakeModule? _keepAwakeModule;
    private UtilitiesModule? _utilitiesModule;
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

        ThemeService themeService = new();
        themeService.Apply(settingsService.Settings.Theme);
        IMotionService motionService = new MotionService(settingsService.Settings.ReducedMotion);

        bool backgroundStartup = e.Args.Any(argument =>
            string.Equals(argument, "--background-network-monitor", StringComparison.Ordinal));
        IModuleRegistry moduleRegistry = new ModuleRegistry();
        INavigationService navigationService = new NavigationService();
        IShutdownService shutdownService = new ShutdownService();
        ClipboardPlusModule clipboardPlusModule = new(settingsService);

        moduleRegistry.Register(new ShutdownModule(shutdownService, settingsService));
        moduleRegistry.Register(new InstalledAppsModule());
        moduleRegistry.Register(clipboardPlusModule);
        moduleRegistry.Register(new TextToolsModule());
        moduleRegistry.Register(new FileToolsModule());
        QuickLaunchModule quickLaunchModule = new(settingsService);
        _quickLaunchModule = quickLaunchModule;
        moduleRegistry.Register(quickLaunchModule);
        moduleRegistry.Register(new WindowToolsModule());
        _lockInspectorModule = new LockInspectorModule();
        moduleRegistry.Register(_lockInspectorModule);
        _keepAwakeModule = new KeepAwakeModule(settingsService);
        moduleRegistry.Register(_keepAwakeModule);
        _utilitiesModule = new UtilitiesModule();
        moduleRegistry.Register(_utilitiesModule);
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
            themeService,
            motionService,
            quickLaunchModule.SetHotkeyEnabled);

        MainWindow window = new(themeService, motionService)
        {
            DataContext = mainViewModel
        };

        MainWindow = window;
        window.Closing += (_, closingEventArgs) =>
        {
            bool keepNetwork = settingsService.Settings.NetworkTrafficContinueInBackground &&
                networkTrafficModule.CurrentViewModel?.IsMonitoring == true;
            bool keepClipboard = clipboardPlusModule.CurrentViewModel?.IsActiveInBackground == true;
            bool keepAwake = _keepAwakeModule.KeepInTray;
            if (_isExplicitExit || (!keepNetwork && !keepClipboard && !keepAwake))
                return;

            closingEventArgs.Cancel = true;
            window.Hide();
            ShowTrayIcon(window, networkTrafficModule, clipboardPlusModule);
        };
        window.Show();
        mainViewModel.Start();
        quickLaunchModule.HotkeyService.Pressed += (_, _) =>
        {
            window.Show();
            if (window.WindowState == WindowState.Minimized)
                window.WindowState = WindowState.Normal;
            window.Activate();
            mainViewModel.NavigateTo("quick-launch");
            quickLaunchModule.CurrentViewModel?.RequestFocusSearch();
        };
        quickLaunchModule.SetHotkeyEnabled(settingsService.Settings.QuickLaunchHotkeyEnabled);

        if (backgroundStartup)
        {
            window.Hide();
            ShowTrayIcon(window, networkTrafficModule, clipboardPlusModule);
            NetworkTrafficViewModel viewModel = (NetworkTrafficViewModel)networkTrafficModule.CreateViewModel();
            _ = viewModel.StartAsyncForBackgroundAsync();
        }
    }

    protected override void OnExit(ExitEventArgs e)
    {
        _keepAwakeModule?.Dispose();
        _utilitiesModule?.Dispose();
        _lockInspectorModule?.Dispose();
        _quickLaunchModule?.Dispose();
        _trayIcon?.Dispose();
        _trayIcon = null;
        base.OnExit(e);
    }

    private void ShowTrayIcon(MainWindow window, NetworkTrafficModule networkTrafficModule, ClipboardPlusModule clipboardPlusModule)
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
            if (_keepAwakeModule is not null)
                await _keepAwakeModule.Service.StopAsync();
            if (networkTrafficModule.CurrentViewModel is not null)
                await networkTrafficModule.CurrentViewModel.StopAsyncForExitAsync();
            clipboardPlusModule.CurrentViewModel?.StopForExit();
            _trayIcon?.Dispose();
            _trayIcon = null;
            window.Close();
        });
        _trayIcon = new Forms.NotifyIcon
        {
            Icon = System.Drawing.SystemIcons.Application,
            Text = "Windows 工具箱正在后台运行",
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
