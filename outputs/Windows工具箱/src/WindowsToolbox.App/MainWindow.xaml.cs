using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Threading;
using WindowsToolbox.App.Services;
using WindowsToolbox.App.Utilities;
using WindowsToolbox.App.ViewModels;
using WindowsToolbox.Core.Interfaces;
using WindowsToolbox.Core.Models;

namespace WindowsToolbox.App;

public partial class MainWindow : Window
{
    private readonly ThemeService? _themeService;
    private readonly IMotionService? _motionService;
    private MainWindowViewModel? _viewModel;
    private WindowState _lastWindowState;
    private int _windowTransitionVersion;

    public MainWindow(ThemeService? themeService = null, IMotionService? motionService = null)
    {
        _themeService = themeService;
        _motionService = motionService;
        InitializeComponent();
        Loaded += MainWindow_Loaded;
        Closed += MainWindow_Closed;
        DataContextChanged += MainWindow_DataContextChanged;
        StateChanged += MainWindow_StateChanged;
        if (_themeService is not null)
        {
            _themeService.ThemeChanging += ThemeService_ThemeChanging;
            _themeService.ThemeChanged += ThemeService_ThemeChanged;
        }
        if (_motionService is not null)
        {
            _motionService.Changed += MotionService_Changed;
            ApplyMotionResources();
        }
    }

    private void MainWindow_Loaded(object sender, RoutedEventArgs e)
    {
        _lastWindowState = WindowState;
        if (_viewModel is not null)
            AnimateSidebar(_viewModel.IsSidebarExpanded, immediate: true);
        ResetContentTransition();
    }

    private void MainWindow_Closed(object? sender, EventArgs e)
    {
        if (_viewModel is not null)
            _viewModel.PropertyChanged -= ViewModel_PropertyChanged;
        if (_themeService is not null)
        {
            _themeService.ThemeChanging -= ThemeService_ThemeChanging;
            _themeService.ThemeChanged -= ThemeService_ThemeChanged;
        }
        if (_motionService is not null)
            _motionService.Changed -= MotionService_Changed;
    }

    private void MainWindow_DataContextChanged(object sender, DependencyPropertyChangedEventArgs e)
    {
        if (_viewModel is not null)
            _viewModel.PropertyChanged -= ViewModel_PropertyChanged;

        _viewModel = e.NewValue as MainWindowViewModel;
        if (_viewModel is not null)
        {
            _viewModel.PropertyChanged += ViewModel_PropertyChanged;
            AnimateSidebar(_viewModel.IsSidebarExpanded, immediate: !IsLoaded);
        }
    }

    private void ViewModel_PropertyChanged(object? sender, System.ComponentModel.PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(MainWindowViewModel.IsSidebarExpanded) && _viewModel is not null)
            AnimateSidebar(_viewModel.IsSidebarExpanded, immediate: false);
    }

    private void MotionService_Changed(object? sender, EventArgs e)
    {
        ApplyMotionResources();
        if (IsLoaded && _viewModel is not null && _motionService?.CurrentMode == ReducedMotionMode.Off)
            AnimateSidebar(_viewModel.IsSidebarExpanded, immediate: true);
    }

    private void ApplyMotionResources()
    {
        if (_motionService is null || Application.Current is null)
            return;

        Application.Current.Resources["MotionDurationFast"] = new Duration(_motionService.GetDuration(TimeSpan.FromMilliseconds(120)));
        Application.Current.Resources["MotionDurationNormal"] = new Duration(_motionService.GetDuration(TimeSpan.FromMilliseconds(180)));
        Application.Current.Resources["MotionDurationSlow"] = new Duration(_motionService.GetDuration(TimeSpan.FromMilliseconds(240)));
    }

    private void AnimateSidebar(bool expanded, bool immediate)
    {
        ReducedMotionMode mode = _motionService?.CurrentMode ?? ReducedMotionMode.Off;
        double targetWidth = expanded ? SidebarMotionMetrics.ExpandedWidth : SidebarMotionMetrics.CollapsedWidth;
        double currentWidth = SidebarColumn.Width.IsAbsolute
            ? SidebarColumn.Width.Value
            : SidebarColumn.ActualWidth;
        double currentOffset = MainContentTransform.X;

        // Commit the final geometry once. The content transform carries the visual transition,
        // so the chart/list on the active page is not repeatedly measured during the motion.
        SidebarColumn.BeginAnimation(ColumnDefinition.WidthProperty, null);
        SidebarColumn.Width = new GridLength(targetWidth);
        AnimateSidebarText(expanded, immediate || mode == ReducedMotionMode.Off);

        if (immediate || mode == ReducedMotionMode.Off)
        {
            MainContentTransform.BeginAnimation(TranslateTransform.XProperty, null);
            MainContentTransform.X = 0;
            return;
        }

        double startOffset = SidebarMotionMetrics.CalculateContentStartOffset(
            currentWidth,
            currentOffset,
            targetWidth);
        MainContentTransform.BeginAnimation(TranslateTransform.XProperty, null);
        MainContentTransform.X = startOffset;

        TimeSpan duration = SidebarMotionMetrics.Scale(SidebarMotionMetrics.GeometryDuration, mode);
        TimeSpan beginTime = expanded
            ? TimeSpan.Zero
            : SidebarMotionMetrics.Scale(SidebarMotionMetrics.TextFadeOutDuration, mode);
        DoubleAnimation animation = new(0, new Duration(duration))
        {
            BeginTime = beginTime,
            EasingFunction = (IEasingFunction?)TryFindResource("StandardEase"),
            FillBehavior = FillBehavior.HoldEnd
        };
        MainContentTransform.BeginAnimation(TranslateTransform.XProperty, animation, HandoffBehavior.SnapshotAndReplace);
    }

    private void AnimateSidebarText(bool expanded, bool immediate)
    {
        foreach (TextBlock textBlock in FindSidebarTextBlocks())
        {
            double currentOpacity = textBlock.Opacity;
            textBlock.BeginAnimation(UIElement.OpacityProperty, null);
            textBlock.Opacity = currentOpacity;

            if (immediate)
            {
                textBlock.Opacity = expanded ? 1 : 0;
                continue;
            }

            ReducedMotionMode mode = _motionService?.CurrentMode ?? ReducedMotionMode.Off;
            TimeSpan duration = SidebarMotionMetrics.Scale(
                expanded ? SidebarMotionMetrics.TextFadeInDuration : SidebarMotionMetrics.TextFadeOutDuration,
                mode);
            TimeSpan beginTime = expanded
                ? SidebarMotionMetrics.Scale(SidebarMotionMetrics.RecentExpandDelay, mode)
                : TimeSpan.Zero;
            textBlock.BeginAnimation(UIElement.OpacityProperty, new DoubleAnimation(
                expanded ? 1 : 0,
                new Duration(duration))
            {
                BeginTime = beginTime,
                EasingFunction = (IEasingFunction?)TryFindResource("StandardEase"),
                FillBehavior = FillBehavior.HoldEnd
            }, HandoffBehavior.SnapshotAndReplace);
        }
    }

    private IEnumerable<TextBlock> FindSidebarTextBlocks()
    {
        foreach (DependencyObject child in EnumerateVisualChildren(SidebarContainer))
        {
            if (child is not TextBlock textBlock)
                continue;

            string source = textBlock.FontFamily?.Source ?? string.Empty;
            if (!source.Contains("Fluent Icons", StringComparison.OrdinalIgnoreCase))
                yield return textBlock;
        }
    }

    private static IEnumerable<DependencyObject> EnumerateVisualChildren(DependencyObject parent)
    {
        for (int index = 0; index < VisualTreeHelper.GetChildrenCount(parent); index++)
        {
            DependencyObject child = VisualTreeHelper.GetChild(parent, index);
            yield return child;
            foreach (DependencyObject descendant in EnumerateVisualChildren(child))
                yield return descendant;
        }
    }

    private void ThemeService_ThemeChanging(object? sender, EventArgs e)
    {
        ThemeTransitionOverlay.BeginAnimation(OpacityProperty, null);
        if (_motionService?.CurrentMode == ReducedMotionMode.Off)
        {
            ThemeTransitionOverlay.Opacity = 0;
            return;
        }

        ThemeTransitionOverlay.Opacity = 1;
    }

    private void ThemeService_ThemeChanged(object? sender, EventArgs e)
    {
        ThemeTransitionOverlay.Background = (Brush)FindResource("AppBackgroundBrush");
        if (_motionService?.CurrentMode == ReducedMotionMode.Off)
        {
            ThemeTransitionOverlay.Opacity = 0;
            return;
        }

        DoubleAnimation fadeOut = new(1, 0, new Duration(_motionService?.GetDuration(TimeSpan.FromMilliseconds(200)) ?? TimeSpan.FromMilliseconds(200)))
        {
            EasingFunction = (IEasingFunction?)TryFindResource("EmphasizedEase")
        };
        ThemeTransitionOverlay.BeginAnimation(OpacityProperty, fadeOut, HandoffBehavior.SnapshotAndReplace);
    }

    private void MainWindow_StateChanged(object? sender, EventArgs e)
    {
        WindowState previousState = _lastWindowState;
        _lastWindowState = WindowState;
        if (!IsLoaded || previousState == WindowState || !IsMaximizeRestoreTransition(previousState, WindowState))
            return;

        int transitionVersion = ++_windowTransitionVersion;
        Dispatcher.BeginInvoke(DispatcherPriority.Render, new Action(() =>
        {
            if (transitionVersion == _windowTransitionVersion)
                AnimateContentTransition();
        }));
    }

    private static bool IsMaximizeRestoreTransition(WindowState previousState, WindowState currentState) =>
        (previousState == WindowState.Normal && currentState == WindowState.Maximized) ||
        (previousState == WindowState.Maximized && currentState == WindowState.Normal);

    private void AnimateContentTransition()
    {
        if (!IsLoaded || _motionService?.CurrentMode == ReducedMotionMode.Off)
        {
            MainContentHost.BeginAnimation(OpacityProperty, null);
            MainContentHost.Opacity = 1;
            return;
        }

        Duration duration = new(SidebarMotionMetrics.Scale(TimeSpan.FromMilliseconds(100), _motionService?.CurrentMode ?? ReducedMotionMode.Full));
        MainContentHost.BeginAnimation(OpacityProperty, new DoubleAnimation(0.97, 1, duration)
        {
            EasingFunction = (IEasingFunction?)TryFindResource("StandardEase")
        }, HandoffBehavior.SnapshotAndReplace);
    }

    private void ResetContentTransition()
    {
        MainContentHost.BeginAnimation(OpacityProperty, null);
        MainContentHost.Opacity = 1;
    }

    private void TitleBar_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        if (e.ChangedButton != MouseButton.Left)
            return;

        if (e.ClickCount == 2)
            ToggleMaximize();
        else if (e.ButtonState == MouseButtonState.Pressed)
            DragMove();
    }

    private void MinimizeButton_Click(object sender, RoutedEventArgs e) =>
        WindowState = WindowState.Minimized;

    private void MaximizeButton_Click(object sender, RoutedEventArgs e) =>
        ToggleMaximize();

    private void CloseButton_Click(object sender, RoutedEventArgs e) =>
        Close();

    private void ToggleMaximize() =>
        WindowState = WindowState == WindowState.Maximized
            ? WindowState.Normal
            : WindowState.Maximized;
}
