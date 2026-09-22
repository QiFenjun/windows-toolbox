using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using WindowsToolbox.Modules.QuickLaunch.Models;

namespace WindowsToolbox.Modules.QuickLaunch.Services;

public sealed class WindowsQuickLaunchExecutor : IQuickLaunchExecutor
{
    public Task<QuickLaunchLaunchResult> LaunchAsync(QuickLaunchItem item, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        if (!QuickLaunchItemRules.IsValidTarget(item.Type, item.Target))
            return Task.FromResult(new QuickLaunchLaunchResult(false, "目标格式无效。"));
        if (item.Type != QuickLaunchItemType.Url && !QuickLaunchItemRules.TargetExists(item))
            return Task.FromResult(new QuickLaunchLaunchResult(false, "目标不存在。"));

        try
        {
            ProcessStartInfo startInfo = new()
            {
                FileName = item.Target,
                UseShellExecute = true,
                WorkingDirectory = item.Type == QuickLaunchItemType.Application && Directory.Exists(item.WorkingDirectory)
                    ? item.WorkingDirectory
                    : string.Empty
            };
            if (item.Type == QuickLaunchItemType.Application && !string.IsNullOrWhiteSpace(item.Arguments))
                startInfo.Arguments = item.Arguments;
            Process.Start(startInfo);
            return Task.FromResult(new QuickLaunchLaunchResult(true, "已打开。"));
        }
        catch (Exception exception) when (exception is InvalidOperationException or Win32Exception)
        {
            return Task.FromResult(new QuickLaunchLaunchResult(false, "无法打开此项目。"));
        }
    }

    public Task<QuickLaunchLaunchResult> OpenLocationAsync(QuickLaunchItem item, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        if (item.Type == QuickLaunchItemType.Url)
            return Task.FromResult(new QuickLaunchLaunchResult(false, "网页没有本地所在位置。"));

        string? location = item.Type == QuickLaunchItemType.Folder
            ? item.Target
            : Path.GetDirectoryName(item.Target);
        if (string.IsNullOrWhiteSpace(location) || !Directory.Exists(location))
            return Task.FromResult(new QuickLaunchLaunchResult(false, "所在位置不存在。"));

        try
        {
            Process.Start(new ProcessStartInfo { FileName = location, UseShellExecute = true });
            return Task.FromResult(new QuickLaunchLaunchResult(true, "已打开所在位置。"));
        }
        catch (Exception exception) when (exception is InvalidOperationException or Win32Exception)
        {
            return Task.FromResult(new QuickLaunchLaunchResult(false, "无法打开所在位置。"));
        }
    }
}
