using System.Diagnostics;
using System.Text.Json;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Media;
using System.Windows.Threading;
using WindowsToolbox.App;
using WindowsToolbox.App.Services;
using WindowsToolbox.App.ViewModels;
using WindowsToolbox.Core.Interfaces;
using WindowsToolbox.Core.Models;
using WindowsToolbox.Core.Services;

internal static class NavigationStress
{
    private static readonly string[] Routes =
    [
        "home", "shutdown", "installed-apps", "clipboard-plus", "text-tools", "file-tools",
        "quick-launch", "window-tools", "lock-inspector", "keep-awake", "utilities",
        "network-traffic", "settings", "utilities"
    ];

    public static void Run()
    {
        Exception? failure = null;
        string tempDirectory = Path.Combine(Path.GetTempPath(), "WindowsToolbox.NavigationStress", Guid.NewGuid().ToString("N"));
        SettingsService settings = new(Path.Combine(tempDirectory, "settings.json"));
        NavigationReport? report = null;
        Thread thread = new(() =>
        {
            try { report = RunOnDispatcher(settings); }
            catch (Exception exception) { failure = exception; }
        }) { IsBackground = true, Name = "Navigation stress WPF dispatcher" };
        thread.SetApartmentState(ApartmentState.STA);
        thread.Start();
        thread.Join();

        try { settings.SaveAsync().GetAwaiter().GetResult(); }
        catch (Exception exception) { failure ??= exception; }

        try
        {
            if (File.Exists(settings.SettingsFilePath))
                _ = JsonDocument.Parse(File.ReadAllBytes(settings.SettingsFilePath));
        }
        catch (JsonException exception) { failure ??= new InvalidOperationException("Concurrent settings writes produced invalid JSON.", exception); }
        finally
        {
            if (Directory.Exists(tempDirectory)) Directory.Delete(tempDirectory, recursive: true);
        }

        if (report is null) throw new InvalidOperationException("Navigation stress did not produce a report.", failure);
        Console.WriteLine($"Navigation stress: requests={report.Requests}, completed={report.Completed}, stale={report.Stale}, cancelled={report.Cancelled}, failed={report.Failed}, cache hits={report.CacheHits}, longest UI heartbeat gap={report.LongestHeartbeatGap.TotalMilliseconds:F0} ms, slowest navigate+layout={report.SlowestNavigation.TotalMilliseconds:F0} ms, theme switches during nav={report.ThemeSwitches}, language/menu/single-language refresh={report.LanguageRefreshPassed}, ComboBox templates={report.SettingsComboSmokePassed}, sidebar collapse/center={report.SidebarGeometryPassed}, unhandled exceptions={report.UnhandledExceptions} ({report.UnhandledExceptionTypes}), final page={report.FinalPageId}, animation A/B={report.AnimationOnRequests}/{report.AnimationOffRequests} requests.");
        if (failure is not null || report.Failed != 0 || report.UnhandledExceptions != 0 || report.Completed != report.Requests || report.Stale != 0 || report.LongestHeartbeatGap > TimeSpan.FromMilliseconds(1000) || report.FinalPageId != Routes[(50 - 1) % Routes.Length] || !report.LanguageRefreshPassed || !report.SettingsComboSmokePassed || !report.SidebarGeometryPassed)
            throw new InvalidOperationException("Navigation stress failed.", failure);
        Console.WriteLine("PASS: real offscreen WPF shell and Dispatcher; 50 rapid route changes with motion enabled and 50 with motion disabled; temporary settings file only.");
    }

    private static NavigationReport RunOnDispatcher(SettingsService settings)
    {
        App app = new();
        app.InitializeComponent();
        NavigationService navigation = new();
        LocalizationService localization = new();
        localization.Apply(settings.Settings.Language);
        List<NavigationDiagnosticEventArgs> navigationEvents = [];
        navigation.Diagnostic += (_, eventArgs) => navigationEvents.Add(eventArgs);
        int unhandled = 0;
        List<string> unhandledTypes = [];
        app.DispatcherUnhandledException += (_, eventArgs) =>
        {
            unhandled++;
            unhandledTypes.Add($"{navigation.CurrentPageId}: {eventArgs.Exception.GetType().Name}: {eventArgs.Exception.Message}");
            eventArgs.Handled = true;
        };

        ModuleRegistry modules = new();
        foreach (string id in Routes.Where(id => id is not "home" and not "settings" and not "about").Distinct(StringComparer.OrdinalIgnoreCase))
            modules.Register(new ProbeModule(id));

        MotionService motion = new(ReducedMotionMode.Full);
        settings.Settings.Theme = ThemeMode.Dark;
        ThemeService theme = new();
        theme.Apply(ThemeMode.Dark);
        MainWindowViewModel viewModel = new(modules, navigation, settings, theme, motion, localization);
        MainWindow window = new(theme, motion)
        {
            DataContext = viewModel,
            ShowActivated = false,
            ShowInTaskbar = false,
            WindowStartupLocation = WindowStartupLocation.Manual,
            Left = -12000,
            Top = -12000
        };

        int requests = 0;
        int completed = 0;
        int stale = 0;
        int cancelled = 0;
        int failed = 0;
        SettingsViewModel? cachedSettings = null;
        bool collapsedCentered = false;
        bool expandedCentered = false;
        bool settingsComboChinesePassed = false;
        bool settingsComboEnglishPassed = false;
        bool singleLanguageShell = false;
        int themeSwitches = 0;
        TimeSpan slowest = TimeSpan.Zero;
        TimeSpan longestGap = TimeSpan.Zero;
        long previousBeat = 0;
        DispatcherTimer heartbeat = new(DispatcherPriority.Background, window.Dispatcher)
        {
            Interval = TimeSpan.FromMilliseconds(250)
        };
        heartbeat.Tick += (_, _) =>
        {
            long now = Stopwatch.GetTimestamp();
            TimeSpan gap = Stopwatch.GetElapsedTime(previousBeat, now);
            if (gap > longestGap) longestGap = gap;
            previousBeat = now;
        };
        window.Show();
        window.UpdateLayout();
        PumpDispatcher(window.Dispatcher);
        previousBeat = Stopwatch.GetTimestamp();
        heartbeat.Start();

        void NavigateBatch(ReducedMotionMode mode)
        {
            motion.SetMode(mode);
            viewModel.ToggleSidebarCommand.Execute(null);
            window.UpdateLayout();
            if (window.FindName("SidebarColumn") is ColumnDefinition sidebarColumn)
            {
                double expectedWidth = mode == ReducedMotionMode.Full ? 72 : 232;
                bool centered = Math.Abs(sidebarColumn.Width.Value - expectedWidth) < 0.1 &&
                    (mode != ReducedMotionMode.Full || IsCollapsedModuleIconCentered(window));
                if (mode == ReducedMotionMode.Full) collapsedCentered = centered;
                else expandedCentered = centered;
            }
            for (int index = 0; index < 50; index++)
            {
                if (index > 0 && index % 25 == 0)
                {
                    viewModel.ToggleThemeCommand.Execute(null);
                    themeSwitches++;
                    window.UpdateLayout();
                }
                string route = Routes[index % Routes.Length];
                requests++;
                Stopwatch elapsed = Stopwatch.StartNew();
                try
                {
                    viewModel.NavigateTo(route);
                    window.UpdateLayout();
                    if (route == "home" && localization.IsEnglish)
                    {
                        string[] renderedText = FindVisualChildren<TextBlock>(window).Select(text => text.Text).ToArray();
                        singleLanguageShell = renderedText.Contains("Window Tools") && renderedText.Contains("Shutdown Timer") &&
                            renderedText.Contains("System Tools") && renderedText.Contains("Open tool") &&
                            !renderedText.Contains("窗口工具") && !renderedText.Contains("定时关机") &&
                            !renderedText.Contains("系统工具") && !renderedText.Contains("打开工具") &&
                            window.FindName("PageHeaderPanel") is StackPanel { Children.Count: 2 };
                    }
                    if (route == "settings") cachedSettings = viewModel.CurrentContent as SettingsViewModel;
                    if (route == "settings" && cachedSettings is not null)
                    {
                        bool passed = CheckSettingsCombos(window, cachedSettings, localization.IsEnglish);
                        if (localization.IsEnglish) settingsComboEnglishPassed = passed;
                        else settingsComboChinesePassed = passed;
                    }
                    if (viewModel.CurrentPageId != route) stale++;
                    else completed++;
                }
                catch (OperationCanceledException) { cancelled++; }
                catch (Exception) { failed++; }
                elapsed.Stop();
                if (elapsed.Elapsed > slowest) slowest = elapsed.Elapsed;
                PumpDispatcher(window.Dispatcher);
            }
        }

        NavigateBatch(ReducedMotionMode.Full);
        bool languageMenuPassed = CheckLanguageMenu(window, viewModel, english: false);
        if (window.FindName("LanguageButton") is Button languageButton && languageButton.ContextMenu is ContextMenu languageMenu &&
            languageMenu.Items.OfType<MenuItem>().LastOrDefault() is MenuItem englishItem)
            englishItem.Command?.Execute(englishItem.CommandParameter);
        languageMenuPassed &= viewModel.IsEnglishLanguage && CheckLanguageMenu(window, viewModel, english: true);
        window.UpdateLayout();
        NavigateBatch(ReducedMotionMode.Off);
        bool languageRefreshPassed = viewModel.LanguageBadge == "EN" &&
            viewModel.CurrentTitle == "Window Tools" &&
            languageMenuPassed && singleLanguageShell &&
            viewModel.IsEnglishLanguage && !viewModel.IsChineseLanguage &&
            cachedSettings is not null &&
            cachedSettings.ThemeOptions.Any(option => option.DisplayName == "Dark") &&
            cachedSettings.StartupOptions.Any(option => option.DisplayName == "Home") &&
            viewModel.Modules.Concat(viewModel.EfficiencyModules)
                .Single(module => module.Id == "window-tools").DisplayName == "Window Tools";

        DispatcherFrame frame = new();
        DispatcherTimer settle = new(TimeSpan.FromSeconds(1), DispatcherPriority.Background,
            (_, _) => frame.Continue = false, window.Dispatcher);
        settle.Start();
        Dispatcher.PushFrame(frame);
        settle.Stop();
        heartbeat.Stop();
        window.Close();
        app.Shutdown();

        return new(requests, completed, stale, cancelled, failed, longestGap, slowest,
            unhandled, string.Join(" | ", unhandledTypes), viewModel.CurrentPageId, 50, 50,
            navigationEvents.Count(e => e.Stage == NavigationDiagnosticStage.ViewModelResolved && e.CacheHit), themeSwitches,
            languageRefreshPassed, settingsComboChinesePassed && settingsComboEnglishPassed, collapsedCentered && expandedCentered);
    }

    private static void PumpDispatcher(Dispatcher dispatcher)
    {
        DispatcherFrame frame = new();
        dispatcher.BeginInvoke(DispatcherPriority.ApplicationIdle, new Action(() => frame.Continue = false));
        Dispatcher.PushFrame(frame);
    }

    private static bool CheckLanguageMenu(MainWindow window, MainWindowViewModel viewModel, bool english)
    {
        if (window.FindName("LanguageButton") is not Button button || button.ContextMenu is not ContextMenu menu)
            return false;
        button.RaiseEvent(new RoutedEventArgs(System.Windows.Controls.Primitives.ButtonBase.ClickEvent));
        MenuItem[] items = menu.Items.OfType<MenuItem>().ToArray();
        bool selection = menu.IsOpen && ReferenceEquals(menu.DataContext, viewModel) && items.Length == 2 &&
            items[0].Header?.ToString() == (english ? "简体中文" : "✓  简体中文") &&
            items[1].Header?.ToString() == (english ? "✓  English" : "English") &&
            items[0].Command is not null && items[0].CommandParameter?.ToString() == LocalizationService.Chinese;
        menu.IsOpen = false;
        return selection;
    }

    private static bool CheckSettingsCombos(DependencyObject root, SettingsViewModel settings, bool english)
    {
        ComboBox[] combos = FindVisualChildren<ComboBox>(root).ToArray();
        bool labelsLocalized = english
            ? settings.ThemeOptions.Any(option => option.DisplayName == "Dark") &&
              settings.MotionOptions.Any(option => option.DisplayName == "Reduced") &&
              settings.StartupOptions.Any(option => option.Id == "home" && option.DisplayName == "Home")
            : settings.ThemeOptions.Any(option => option.DisplayName == "深色模式") &&
              settings.MotionOptions.Any(option => option.DisplayName == "减少") &&
              settings.StartupOptions.Any(option => option.Id == "home" && option.DisplayName == "首页");
        return labelsLocalized && combos.Length == 3 &&
            IsLocalizedCombo(combos[0], settings.ThemeOptions, settings.SelectedTheme) &&
            IsLocalizedCombo(combos[1], settings.MotionOptions, settings.SelectedMotion) &&
            IsLocalizedCombo(combos[2], settings.StartupOptions, settings.SelectedStartupPage);
    }

    private static bool IsLocalizedCombo<T>(ComboBox combo, IReadOnlyList<T> options, T selected)
    {
        TextBlock? itemText = combo.ItemTemplate?.LoadContent() as TextBlock;
        return ReferenceEquals(combo.ItemsSource, options) && Equals(combo.SelectedItem, selected) &&
            TextSearch.GetTextPath(combo) == "DisplayName" &&
            BindingOperations.GetBinding(itemText, TextBlock.TextProperty)?.Path.Path == "DisplayName";
    }

    private static bool IsCollapsedModuleIconCentered(MainWindow window)
    {
        Button? moduleButton = FindVisualChildren<Button>(window)
            .FirstOrDefault(button => button.CommandParameter is string id && id == "window-tools");
        TextBlock? icon = moduleButton is null ? null : FindVisualChildren<TextBlock>(moduleButton).FirstOrDefault();
        if (moduleButton is null || icon is null || moduleButton.ActualWidth <= 0) return false;
        double iconCenter = icon.TranslatePoint(new Point(icon.ActualWidth / 2, 0), moduleButton).X;
        return Math.Abs(iconCenter - moduleButton.ActualWidth / 2) < 1.5;
    }

    private static IEnumerable<T> FindVisualChildren<T>(DependencyObject root) where T : DependencyObject
    {
        for (int index = 0; index < VisualTreeHelper.GetChildrenCount(root); index++)
        {
            DependencyObject child = VisualTreeHelper.GetChild(root, index);
            if (child is T matched) yield return matched;
            foreach (T descendant in FindVisualChildren<T>(child)) yield return descendant;
        }
    }

    private sealed record NavigationReport(
        int Requests,
        int Completed,
        int Stale,
        int Cancelled,
        int Failed,
        TimeSpan LongestHeartbeatGap,
        TimeSpan SlowestNavigation,
        int UnhandledExceptions,
        string UnhandledExceptionTypes,
        string FinalPageId,
        int AnimationOnRequests,
        int AnimationOffRequests,
        int CacheHits,
        int ThemeSwitches,
        bool LanguageRefreshPassed,
        bool SettingsComboSmokePassed,
        bool SidebarGeometryPassed);

    private sealed class ProbeModule(string id) : IToolModule
    {
        public string Id { get; } = id;
        public string DisplayName => Id switch
        {
            "window-tools" => "窗口工具",
            "network-traffic" => "网络流量",
            "installed-apps" => "应用管理",
            "lock-inspector" => "占用检测",
            "keep-awake" => "保持唤醒",
            "quick-launch" => "快捷启动",
            "clipboard-plus" => "剪贴板+",
            "text-tools" => "文本工具",
            "file-tools" => "文件工具",
            "shutdown" => "定时关机",
            "utilities" => "小工具",
            _ => Id
        };
        public string EnglishName => Id switch
        {
            "window-tools" => "Window Tools",
            "network-traffic" => "Network Traffic",
            "installed-apps" => "App Manager",
            "lock-inspector" => "Lock Inspector",
            "keep-awake" => "Keep Awake",
            "quick-launch" => "Quick Launch",
            "clipboard-plus" => "Clipboard+",
            "text-tools" => "Text Tools",
            "file-tools" => "File Tools",
            "shutdown" => "Shutdown Timer",
            "utilities" => "Utilities",
            _ => Id
        };
        public string Description => Id;
        public string Category => Id.StartsWith("network", StringComparison.Ordinal) ? "系统工具" : "效率工具";
        public string EnglishCategory => Id.StartsWith("network", StringComparison.Ordinal) ? "System Tools" : "Productivity Tools";
        public string IconKey => "GridView";
        public int SortOrder => 0;
        public bool IsAvailable => true;
        public IReadOnlyList<string> Keywords => [Id];
        public string? ResourceDictionaryPath => null;
        public object CreateViewModel() => new ProbeViewModel(Id);
    }

    private sealed record ProbeViewModel(string Id)
    {
        public override string ToString() => Id;
    }

}
