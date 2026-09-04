using System.ComponentModel;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Animation;
using WindowsToolbox.App.Services;
using WindowsToolbox.App.Utilities;
using WindowsToolbox.App.ViewModels;
using WindowsToolbox.Core.Models;

namespace WindowsToolbox.App.Views;

public partial class HomeView : UserControl
{
    private HomeViewModel? _viewModel;
    private int _recentTransitionVersion;

    public HomeView()
    {
        InitializeComponent();
        Loaded += HomeView_Loaded;
        Unloaded += HomeView_Unloaded;
        DataContextChanged += HomeView_DataContextChanged;
    }

    private void HomeView_Loaded(object sender, RoutedEventArgs e)
    {
        AttachViewModel(DataContext as HomeViewModel);
        ApplyRecentState(_viewModel?.HasRecentModules == true, immediate: true);
    }

    private void HomeView_Unloaded(object sender, RoutedEventArgs e)
    {
        DetachViewModel();
    }

    private void HomeView_DataContextChanged(object sender, DependencyPropertyChangedEventArgs e)
    {
        DetachViewModel();
        if (IsLoaded)
            AttachViewModel(e.NewValue as HomeViewModel);
    }

    private void AttachViewModel(HomeViewModel? viewModel)
    {
        if (ReferenceEquals(_viewModel, viewModel))
            return;

        DetachViewModel();
        _viewModel = viewModel;
        if (_viewModel is null)
            return;

        _viewModel.PropertyChanged += ViewModel_PropertyChanged;
        _viewModel.MotionService.Changed += MotionService_Changed;
    }

    private void DetachViewModel()
    {
        if (_viewModel is null)
            return;

        _viewModel.PropertyChanged -= ViewModel_PropertyChanged;
        _viewModel.MotionService.Changed -= MotionService_Changed;
        _viewModel = null;
    }

    private void ViewModel_PropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(HomeViewModel.HasRecentModules))
            ApplyRecentState(_viewModel?.HasRecentModules == true, immediate: false);
    }

    private void MotionService_Changed(object? sender, EventArgs e)
    {
        if (IsLoaded)
            ApplyRecentState(_viewModel?.HasRecentModules == true, immediate: true);
    }

    private void ApplyRecentState(bool hasRecentModules, bool immediate)
    {
        int transitionVersion = ++_recentTransitionVersion;
        StopRecentAnimations();

        ReducedMotionMode mode = _viewModel?.MotionService.CurrentMode ?? ReducedMotionMode.Off;
        if (immediate || mode == ReducedMotionMode.Off)
        {
            SetRecentStateImmediately(hasRecentModules);
            return;
        }

        TimeSpan fadeIn = SidebarMotionMetrics.Scale(SidebarMotionMetrics.RecentFadeInDuration, mode);
        TimeSpan fadeOut = SidebarMotionMetrics.Scale(SidebarMotionMetrics.RecentFadeOutDuration, mode);
        TimeSpan delay = SidebarMotionMetrics.Scale(SidebarMotionMetrics.RecentExpandDelay, mode);
        double offset = SidebarMotionMetrics.RecentTranslateOffset(mode);

        if (hasRecentModules)
        {
            RecentEmptyState.Visibility = Visibility.Visible;
            RecentEmptyState.Opacity = 1;
            RecentModulesItems.Visibility = Visibility.Visible;
            RecentModulesItems.IsHitTestVisible = false;
            RecentModulesItems.Opacity = 0;
            RecentModulesTransform.Y = offset;

            Animate(RecentEmptyState, UIElement.OpacityProperty, 0, fadeOut, TimeSpan.Zero, () =>
            {
                if (transitionVersion == _recentTransitionVersion)
                    RecentEmptyState.Visibility = Visibility.Collapsed;
            });
            Animate(RecentModulesItems, UIElement.OpacityProperty, 1, fadeIn, delay, () =>
            {
                if (transitionVersion == _recentTransitionVersion)
                    RecentModulesItems.IsHitTestVisible = true;
            });
            Animate(RecentModulesTransform, TranslateTransform.YProperty, 0, fadeIn, delay);
            return;
        }

        RecentEmptyState.Visibility = Visibility.Visible;
        RecentEmptyState.Opacity = 0;
        RecentModulesItems.Visibility = Visibility.Visible;
        RecentModulesItems.IsHitTestVisible = true;
        RecentModulesTransform.Y = 0;

        Animate(RecentModulesItems, UIElement.OpacityProperty, 0, fadeOut, TimeSpan.Zero, () =>
        {
            if (transitionVersion != _recentTransitionVersion)
                return;

            RecentModulesItems.IsHitTestVisible = false;
            RecentModulesItems.Visibility = Visibility.Collapsed;
        });
        Animate(RecentEmptyState, UIElement.OpacityProperty, 1, fadeIn, fadeOut);
    }

    private void SetRecentStateImmediately(bool hasRecentModules)
    {
        RecentEmptyState.BeginAnimation(UIElement.OpacityProperty, null);
        RecentModulesItems.BeginAnimation(UIElement.OpacityProperty, null);
        RecentModulesTransform.BeginAnimation(TranslateTransform.YProperty, null);

        if (hasRecentModules)
        {
            RecentEmptyState.Visibility = Visibility.Collapsed;
            RecentEmptyState.Opacity = 0;
            RecentModulesItems.Visibility = Visibility.Visible;
            RecentModulesItems.Opacity = 1;
            RecentModulesItems.IsHitTestVisible = true;
            RecentModulesTransform.Y = 0;
        }
        else
        {
            RecentEmptyState.Visibility = Visibility.Visible;
            RecentEmptyState.Opacity = 1;
            RecentModulesItems.Visibility = Visibility.Collapsed;
            RecentModulesItems.Opacity = 0;
            RecentModulesItems.IsHitTestVisible = false;
            RecentModulesTransform.Y = SidebarMotionMetrics.RecentTranslateOffset(ReducedMotionMode.Full);
        }
    }

    private void StopRecentAnimations()
    {
        RecentEmptyState.BeginAnimation(UIElement.OpacityProperty, null);
        RecentModulesItems.BeginAnimation(UIElement.OpacityProperty, null);
        RecentModulesTransform.BeginAnimation(TranslateTransform.YProperty, null);
    }

    private static void Animate(
        Animatable target,
        DependencyProperty property,
        double to,
        TimeSpan duration,
        TimeSpan beginTime,
        Action? completed = null)
    {
        DoubleAnimation animation = new(to, new Duration(duration))
        {
            BeginTime = beginTime,
            EasingFunction = Application.Current?.TryFindResource("StandardEase") as IEasingFunction,
            FillBehavior = FillBehavior.HoldEnd
        };
        if (completed is not null)
            animation.Completed += (_, _) => completed();

        target.BeginAnimation(property, animation, HandoffBehavior.SnapshotAndReplace);
    }

    private static void Animate(
        UIElement target,
        DependencyProperty property,
        double to,
        TimeSpan duration,
        TimeSpan beginTime,
        Action? completed = null)
    {
        DoubleAnimation animation = new(to, new Duration(duration))
        {
            BeginTime = beginTime,
            EasingFunction = Application.Current?.TryFindResource("StandardEase") as IEasingFunction,
            FillBehavior = FillBehavior.HoldEnd
        };
        if (completed is not null)
            animation.Completed += (_, _) => completed();

        target.BeginAnimation(property, animation, HandoffBehavior.SnapshotAndReplace);
    }
}
