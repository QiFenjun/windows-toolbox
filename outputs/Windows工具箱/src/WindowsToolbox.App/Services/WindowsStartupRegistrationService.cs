using Microsoft.Win32;

namespace WindowsToolbox.App.Services;

/// <summary>只在用户保存明确选择后登记当前用户的网络监控后台启动项。</summary>
public sealed class WindowsStartupRegistrationService
{
    private const string RunKeyPath = @"Software\Microsoft\Windows\CurrentVersion\Run";
    private const string ValueName = "WindowsToolbox.NetworkTraffic";

    public void Apply(bool enabled)
    {
        using RegistryKey? runKey = Registry.CurrentUser.CreateSubKey(RunKeyPath, writable: true);
        if (runKey is null)
            throw new InvalidOperationException("无法打开当前用户的启动项设置。");
        if (!enabled)
        {
            runKey.DeleteValue(ValueName, throwOnMissingValue: false);
            return;
        }

        string executable = Environment.ProcessPath ?? throw new InvalidOperationException("无法找到程序路径。");
        runKey.SetValue(ValueName, $"\"{executable}\" --background-network-monitor", RegistryValueKind.String);
    }
}
