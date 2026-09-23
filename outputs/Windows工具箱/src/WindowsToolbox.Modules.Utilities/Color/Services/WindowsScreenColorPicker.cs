using System.ComponentModel;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Threading;
using WindowsToolbox.Modules.Utilities.Color.Interop;
using WindowsToolbox.Modules.Utilities.Color.Models;
using MediaColor = System.Windows.Media.Color;
using MediaBrushes = System.Windows.Media.Brushes;

namespace WindowsToolbox.Modules.Utilities.Color.Services;

public sealed class WindowsScreenColorPicker(
    IScreenColorSampler sampler,
    Func<Window?> ownerProvider) : IScreenColorPicker
{
    public async Task<MediaColor?> PickAsync(Action<MediaColor> preview, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(preview);
        cancellationToken.ThrowIfCancellationRequested();
        if (!OperatingSystem.IsWindows())
            throw new PlatformNotSupportedException();

        Dispatcher dispatcher = Application.Current?.Dispatcher ?? Dispatcher.CurrentDispatcher;
        if (!dispatcher.CheckAccess())
            throw new InvalidOperationException("屏幕取色必须从 WPF UI 线程启动。");

        Window? owner = ownerProvider();
        using ScreenPickerOverlayWindow overlay = new(sampler, preview);
        DependencyPropertyChangedEventHandler? ownerHidden = null;
        if (owner is not null)
        {
            ownerHidden = (_, _) =>
            {
                if (!owner.IsVisible)
                    overlay.CancelForApplicationExit();
            };
            owner.IsVisibleChanged += ownerHidden;
        }
        CancellationTokenRegistration registration = cancellationToken.Register(() =>
        {
            if (dispatcher.CheckAccess())
                overlay.CancelForApplicationExit();
            else
                dispatcher.BeginInvoke(overlay.CancelForApplicationExit, DispatcherPriority.Send);
        });
        try
        {
            overlay.Show();
            overlay.BeginSampling();
            MediaColor? result = await overlay.Completion.ConfigureAwait(true);
            return result;
        }
        finally
        {
            registration.Dispose();
            if (owner is not null && ownerHidden is not null)
                owner.IsVisibleChanged -= ownerHidden;
            overlay.StopSampling();
            if (overlay.IsVisible)
                overlay.Close();
            if (overlay.ShouldRestoreOwner && !cancellationToken.IsCancellationRequested && owner?.IsVisible == true)
                owner.Activate();
        }
    }
}

internal sealed class ScreenPickerOverlayWindow : Window, IDisposable
{
    private readonly IScreenColorSampler _sampler;
    private readonly Action<MediaColor> _preview;
    private readonly DispatcherTimer _timer;
    private readonly TaskCompletionSource<MediaColor?> _completion = new(TaskCreationOptions.RunContinuationsAsynchronously);
    private ScreenPoint? _lastPoint;
    private bool _finished;
    private bool _disposed;

    internal ScreenPickerOverlayWindow(IScreenColorSampler sampler, Action<MediaColor> preview)
    {
        _sampler = sampler;
        _preview = preview;
        WindowStyle = WindowStyle.None;
        ResizeMode = ResizeMode.NoResize;
        AllowsTransparency = true;
        Background = MediaBrushes.Transparent;
        ShowInTaskbar = false;
        ShowActivated = true;
        Topmost = true;
        Cursor = System.Windows.Input.Cursors.Cross;
        Focusable = true;
        WindowStartupLocation = WindowStartupLocation.Manual;

        int x = ScreenInterop.GetSystemMetrics(ScreenInterop.SmXVirtualScreen);
        int y = ScreenInterop.GetSystemMetrics(ScreenInterop.SmYVirtualScreen);
        int width = ScreenInterop.GetSystemMetrics(ScreenInterop.SmCxVirtualScreen);
        int height = ScreenInterop.GetSystemMetrics(ScreenInterop.SmCyVirtualScreen);
        if (width <= 0 || height <= 0)
            throw new Win32Exception("无法读取虚拟屏幕范围。");
        Left = x;
        Top = y;
        Width = width;
        Height = height;
        SourceInitialized += (_, _) => PositionNativeWindow(x, y, width, height);
        Loaded += (_, _) => PositionNativeWindow(x, y, width, height);
        PreviewKeyDown += (_, e) =>
        {
            if (e.Key == System.Windows.Input.Key.Escape)
            {
                e.Handled = true;
                Finish(null, restoreOwner: true);
            }
        };
        MouseLeftButtonDown += (_, e) =>
        {
            e.Handled = true;
            try { Finish(_sampler.Sample(_sampler.GetCursorPosition()), restoreOwner: true); }
            catch (Exception exception) { Finish(exception); }
        };
        PreviewMouseRightButtonDown += (_, e) =>
        {
            e.Handled = true;
            Finish(null, restoreOwner: true);
        };
        Deactivated += (_, _) =>
        {
            if (!_finished)
                Finish(null, restoreOwner: false);
        };
        Closed += (_, _) =>
        {
            StopSampling();
            if (!_finished)
                Finish(null, restoreOwner: false);
        };
        _timer = new DispatcherTimer(DispatcherPriority.Input, Dispatcher)
        {
            Interval = TimeSpan.FromMilliseconds(16)
        };
        _timer.Tick += Timer_Tick;
    }

    internal Task<MediaColor?> Completion => _completion.Task;
    internal bool ShouldRestoreOwner { get; private set; }

    internal void BeginSampling()
    {
        _timer.Start();
        SampleCursor();
    }

    internal void StopSampling() => _timer.Stop();

    internal void CancelForApplicationExit()
    {
        if (!_finished)
        {
            Finish(null, restoreOwner: false);
            if (IsVisible)
                Close();
        }
    }

    public void Dispose()
    {
        if (_disposed)
            return;
        _disposed = true;
        StopSampling();
        _timer.Tick -= Timer_Tick;
        if (IsVisible)
            Close();
    }

    private void Timer_Tick(object? sender, EventArgs e) => SampleCursor();

    private void SampleCursor()
    {
        if (_finished)
            return;
        try
        {
            ScreenPoint point = _sampler.GetCursorPosition();
            if (_lastPoint == point)
                return;
            _lastPoint = point;
            MediaColor color = _sampler.Sample(point);
            _preview(color);
        }
        catch (Exception exception)
        {
            Finish(exception);
        }
    }

    private void Finish(MediaColor? color, bool restoreOwner)
    {
        if (_finished)
            return;
        _finished = true;
        ShouldRestoreOwner = restoreOwner;
        StopSampling();
        _completion.TrySetResult(color);
    }

    private void Finish(Exception exception)
    {
        if (_finished)
            return;
        _finished = true;
        StopSampling();
        _completion.TrySetException(exception);
    }

    private void PositionNativeWindow(int x, int y, int width, int height)
    {
        if (!ScreenInterop.SetWindowPos(new System.Windows.Interop.WindowInteropHelper(this).Handle,
                ScreenInterop.HwndTopmost, x, y, width, height, ScreenInterop.SwpShowWindow))
            throw new Win32Exception(Marshal.GetLastWin32Error());
    }
}
