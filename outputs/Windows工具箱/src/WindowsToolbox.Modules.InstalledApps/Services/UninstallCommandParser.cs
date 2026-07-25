using System.IO;
using WindowsToolbox.Modules.InstalledApps.Models;

namespace WindowsToolbox.Modules.InstalledApps.Services;

public static class UninstallCommandParser
{
    public static UninstallCommand? Parse(
        string commandLine,
        Func<string, bool>? fileExists = null)
    {
        if (string.IsNullOrWhiteSpace(commandLine) ||
            commandLine.Contains('\r') ||
            commandLine.Contains('\n') ||
            commandLine.IndexOf('\0') >= 0)
        {
            return null;
        }

        string expanded = Environment.ExpandEnvironmentVariables(commandLine.Trim());
        if (!TrySplit(expanded, out string executable, out string arguments))
            return null;

        try
        {
            executable = ResolveKnownExecutable(executable);
        }
        catch (Exception exception) when (
            exception is ArgumentException ||
            exception is NotSupportedException ||
            exception is PathTooLongException)
        {
            return null;
        }

        string executableName = Path.GetFileName(executable);
        string[] forbiddenShells =
        [
            "cmd.exe", "powershell.exe", "pwsh.exe", "wscript.exe",
            "cscript.exe", "mshta.exe"
        ];
        if (!Path.IsPathRooted(executable) ||
            !executable.EndsWith(".exe", StringComparison.OrdinalIgnoreCase) ||
            forbiddenShells.Contains(executableName, StringComparer.OrdinalIgnoreCase))
        {
            return null;
        }

        Func<string, bool> exists = fileExists ?? File.Exists;
        if (!exists(executable))
            return null;
        if (!string.IsNullOrWhiteSpace(arguments) &&
            ApplicationActionService.SplitArguments(arguments).Count == 0)
        {
            return null;
        }

        return new UninstallCommand(executable, arguments);
    }

    private static bool TrySplit(
        string commandLine,
        out string executable,
        out string arguments)
    {
        executable = string.Empty;
        arguments = string.Empty;

        if (commandLine.StartsWith('"'))
        {
            int closingQuote = commandLine.IndexOf('"', 1);
            if (closingQuote <= 1)
                return false;

            executable = commandLine[1..closingQuote].Trim();
            arguments = commandLine[(closingQuote + 1)..].Trim();
            return executable.Length > 0;
        }

        int exeEnd = commandLine.IndexOf(".exe", StringComparison.OrdinalIgnoreCase);
        if (exeEnd < 0)
            return false;

        exeEnd += 4;
        executable = commandLine[..exeEnd].Trim().Trim('"');
        arguments = commandLine[exeEnd..].Trim();
        return executable.Length > 0;
    }

    private static string ResolveKnownExecutable(string executable)
    {
        executable = executable.Trim().Trim('"');
        if (Path.IsPathRooted(executable))
            return Path.GetFullPath(executable);

        string fileName = Path.GetFileName(executable);
        if (!fileName.Equals("msiexec.exe", StringComparison.OrdinalIgnoreCase) &&
            !fileName.Equals("rundll32.exe", StringComparison.OrdinalIgnoreCase))
        {
            return executable;
        }

        string windowsDirectory = Environment.GetFolderPath(Environment.SpecialFolder.Windows);
        return Path.Combine(windowsDirectory, "System32", fileName);
    }
}
