using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Animation;
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
        if (_viewModel is not null)
            AnimateSidebar(_viewModel.IsSidebarExpanded, immediate: true);
        AnimateContentTransition();
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

    private void MotionService_Changed(object? sender, EventArgs e) => ApplyMotionResources();

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
        GridLength target = new(expanded ? 232 : 72);
        if (immediate || _motionService?.CurrentMode == ReducedMotionMode.Off)
        {
            SidebarColumn.BeginAnimation(ColumnDefinition.WidthProperty, null);
            SidebarColumn.Width = target;
            return;
        }

        Duration duration = new(_motionService?.GetDuration(TimeSpan.FromMilliseconds(190)) ?? TimeSpan.FromMilliseconds(190));
        GridLengthAnimation animation = new()
        {
            From = SidebarColumn.Width,
            To = target,
            Duration = duration,
            EasingFunction = (IEasingFunction?)TryFindResource("StandardEase")
        };
        SidebarColumn.BeginAnimation(ColumnDefinition.WidthProperty, animation, HandoffBehavior.SnapshotAndReplace);
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

    private void MainWindow_StateChanged(object? sender, EventArgs e) => AnimateContentTransition();

    private void AnimateContentTransition()
    {
        if (!IsLoaded || _motionService?.CurrentMode == ReducedMotionMode.Off)
        {
            MainContentHost.BeginAnimation(OpacityProperty, null);
            MainContentTransform.BeginAnimation(TranslateTransform.YProperty, null);
            MainContentHost.Opacity = 1;
            MainContentTransform.Y = 0;
            return;
        }

        Duration duration = new(_motionService?.GetDuration(TimeSpan.FromMilliseconds(140)) ?? TimeSpan.FromMilliseconds(140));
        MainContentHost.BeginAnimation(OpacityProperty, new DoubleAnimation(0.94, 1, duration)
        {
            EasingFunction = (IEasingFunction?)TryFindResource("StandardEase")
        }, HandoffBehavior.SnapshotAndReplace);
        MainContentTransform.BeginAnimation(TranslateTransform.YProperty, new DoubleAnimation(3, 0, duration)
        {
            EasingFunction = (IEasingFunction?)TryFindResource("StandardEase")
        }, HandoffBehavior.SnapshotAndReplace);
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
