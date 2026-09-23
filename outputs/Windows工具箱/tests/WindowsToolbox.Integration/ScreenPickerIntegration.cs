using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Interop;
using System.Windows.Media;
using MediaColor = System.Windows.Media.Color;
using WindowsToolbox.Modules.Utilities.Color.Models;
using WindowsToolbox.Modules.Utilities.Color.Services;

internal static class ScreenPickerIntegration
{
    private static readonly MediaColor Expected = MediaColor.FromRgb(10, 20, 30);

    internal static void Run()
    {
        if (!Environment.UserInteractive)
        {
            Console.WriteLine("NOT RUN: no interactive desktop is available.");
            return;
        }

        Exception? failure = null;
        Thread thread = new(() =>
        {
            try { RunOnSta(); }
            catch (Exception exception) { failure = exception; }
        });
        thread.SetApartmentState(ApartmentState.STA);
        thread.Start();
        thread.Join();
        if (failure is not null)
            throw new InvalidOperationException("Real screen sampler integration failed.", failure);
    }

    private static void RunOnSta()
    {
        if (SetThreadDpiAwarenessContext(new nint(-4)) == 0)
            throw new System.ComponentModel.Win32Exception(Marshal.GetLastWin32Error(), "Per-Monitor V2 DPI context could not be enabled for the integration window.");
        Application application = new() { ShutdownMode = ShutdownMode.OnExplicitShutdown };
        Window window = new()
        {
            Width = 260,
            Height = 220,
            WindowStartupLocation = WindowStartupLocation.CenterScreen,
            WindowStyle = WindowStyle.None,
            ResizeMode = ResizeMode.NoResize,
            ShowInTaskbar = false,
            Topmost = true,
            Background = new SolidColorBrush(Expected)
        };
        try
        {
            window.Show();
            window.UpdateLayout();
            window.Activate();
            window.Dispatcher.Invoke(System.Windows.Threading.DispatcherPriority.ApplicationIdle, new Action(() => { }));
            Thread.Sleep(150);
            if (!GetWindowRect(new WindowInteropHelper(window).Handle, out NativeRect rect))
                throw new System.ComponentModel.Win32Exception(Marshal.GetLastWin32Error());

            ScreenPoint point = new((rect.Left + rect.Right) / 2, (rect.Top + rect.Bottom) / 2);
            WindowsScreenColorSampler sampler = new();
            int before = GetGdiCount();
            Stopwatch stopwatch = Stopwatch.StartNew();
            MediaColor sampled = default;
            for (int index = 0; index < 500; index++)
            {
                sampled = sampler.Sample(point);
                if (Math.Abs(sampled.R - Expected.R) > 2 ||
                    Math.Abs(sampled.G - Expected.G) > 2 ||
                    Math.Abs(sampled.B - Expected.B) > 2)
                    throw new InvalidOperationException($"Sampled color {sampled} instead of the test window color {Expected}.");
            }
            stopwatch.Stop();
            int after = GetGdiCount();
            if (after - before > 2)
                throw new InvalidOperationException($"GDI object count grew by {after - before} across 500 samples.");
            Console.WriteLine($"PASS: sampled this test window at physical pixel ({point.X},{point.Y}) 500 times; last={sampled}; elapsed={stopwatch.ElapsedMilliseconds} ms; GDI before/after={before}/{after}. No screenshot or user window was read.");
        }
        finally
        {
            window.Close();
            application.Shutdown();
        }
    }

    private static int GetGdiCount()
    {
        using Process process = Process.GetCurrentProcess();
        return (int)GetGuiResources(process.Handle, 0);
    }

    [DllImport("user32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool GetWindowRect(nint window, out NativeRect rect);

    [DllImport("user32.dll", SetLastError = true)]
    private static extern nint SetThreadDpiAwarenessContext(nint dpiContext);

    [DllImport("user32.dll")]
    private static extern uint GetGuiResources(nint process, uint flags);

    [StructLayout(LayoutKind.Sequential)]
    private struct NativeRect
    {
        public int Left;
        public int Top;
        public int Right;
        public int Bottom;
    }

}
