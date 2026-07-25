using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using WindowsToolbox.Modules.InstalledApps.Models;

namespace WindowsToolbox.Modules.InstalledApps.Services;

public sealed class ApplicationActionService : IApplicationActionService
{
    public bool OpenInstallLocation(
        InstalledApplication application,
        out string errorMessage)
    {
        errorMessage = string.Empty;
        if (string.IsNullOrWhiteSpace(application.InstallLocation) ||
            !Directory.Exists(application.InstallLocation))
        {
            errorMessage = "该软件没有可访问的安装位置。";
            return false;
        }

        try
        {
            ProcessStartInfo startInfo = new()
            {
                FileName = "explorer.exe",
                UseShellExecute = true
            };
            startInfo.ArgumentList.Add(application.InstallLocation);
            Process.Start(startInfo);
            return true;
        }
        catch (Exception exception) when (
            exception is Win32Exception ||
            exception is InvalidOperationException)
        {
            errorMessage = "无法打开安装位置，请检查目录权限。";
            return false;
        }
    }

    public async Task<UninstallResult> UninstallAsync(
        InstalledApplication application,
        CancellationToken cancellationToken)
    {
        if (!application.CanUninstall)
            return UninstallResult.Failed("该软件没有可靠的卸载方式。");

        UninstallCommand? command = UninstallCommandParser.Parse(application.UninstallString);
        if (command is null)
            return UninstallResult.Failed("无法安全解析软件登记的卸载命令。");

        try
        {
            ProcessStartInfo startInfo = new()
            {
                FileName = command.ExecutablePath,
                UseShellExecute = true,
                Verb = application.RequiresElevation ? "runas" : string.Empty,
                WorkingDirectory = Path.GetDirectoryName(command.ExecutablePath) ?? string.Empty
            };

            foreach (string argument in SplitArguments(command.Arguments))
                startInfo.ArgumentList.Add(argument);

            using Process? process = Process.Start(startInfo);
            if (process is null)
                return UninstallResult.Failed("卸载程序未能启动。");

            await process.WaitForExitAsync(cancellationToken);
            return new UninstallResult(
                true,
                true,
                process.ExitCode,
                "卸载程序已经结束。");
        }
        catch (Win32Exception exception) when (exception.NativeErrorCode == 1223)
        {
            return UninstallResult.Failed("已取消管理员权限请求，未启动卸载。");
        }
        catch (OperationCanceledException)
        {
            return new UninstallResult(true, false, null, "卸载程序仍可能正在运行，请稍后刷新列表。");
        }
        catch (Exception exception) when (
            exception is Win32Exception ||
            exception is InvalidOperationException ||
            exception is UnauthorizedAccessException ||
            exception is ArgumentException)
        {
            return UninstallResult.Failed("无法启动软件登记的卸载程序。");
        }
    }

    public static IReadOnlyList<string> SplitArguments(string arguments)
    {
        if (string.IsNullOrWhiteSpace(arguments))
            return [];

        List<string> values = [];
        bool quoted = false;
        System.Text.StringBuilder current = new();
        for (int index = 0; index < arguments.Length; index++)
        {
            char character = arguments[index];
            if (character == '"')
            {
                quoted = !quoted;
                continue;
            }

            if (char.IsWhiteSpace(character) && !quoted)
            {
                if (current.Length > 0)
                {
                    values.Add(current.ToString());
                    current.Clear();
                }
                continue;
            }

            current.Append(character);
        }

        if (quoted)
            return [];
        if (current.Length > 0)
            values.Add(current.ToString());
        return values;
    }
}
