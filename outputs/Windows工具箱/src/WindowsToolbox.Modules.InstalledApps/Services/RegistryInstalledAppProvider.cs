using System.IO;
using Microsoft.Win32;
using WindowsToolbox.Modules.InstalledApps.Models;

namespace WindowsToolbox.Modules.InstalledApps.Services;

public sealed class RegistryInstalledAppProvider : IInstalledAppProvider
{
    private const string UninstallSubKey =
        @"Software\Microsoft\Windows\CurrentVersion\Uninstall";

    private static readonly string[] ValueNames =
    [
        "DisplayName",
        "DisplayVersion",
        "Publisher",
        "InstallDate",
        "InstallLocation",
        "DisplayIcon",
        "EstimatedSize",
        "UninstallString",
        "QuietUninstallString",
        "ModifyPath",
        "WindowsInstaller",
        "SystemComponent",
        "ReleaseType",
        "ParentKeyName",
        "NoRemove"
    ];

    public Task<IReadOnlyList<InstalledApplication>> GetInstalledApplicationsAsync(
        CancellationToken cancellationToken) =>
        Task.Run<IReadOnlyList<InstalledApplication>>(
            () => ReadAll(cancellationToken),
            cancellationToken);

    private static IReadOnlyList<InstalledApplication> ReadAll(
        CancellationToken cancellationToken)
    {
        if (!OperatingSystem.IsWindows())
            return [];

        List<InstalledApplication> applications = [];
        ReadView(RegistryHive.LocalMachine, RegistryView.Registry64, "x64", true, applications, cancellationToken);
        ReadView(RegistryHive.LocalMachine, RegistryView.Registry32, "x86", true, applications, cancellationToken);
        ReadView(RegistryHive.CurrentUser, RegistryView.Registry64, "x64", false, applications, cancellationToken);
        ReadView(RegistryHive.CurrentUser, RegistryView.Registry32, "x86", false, applications, cancellationToken);
        return applications;
    }

    private static void ReadView(
        RegistryHive hive,
        RegistryView view,
        string architecture,
        bool requiresElevation,
        ICollection<InstalledApplication> applications,
        CancellationToken cancellationToken)
    {
        try
        {
            using RegistryKey baseKey = RegistryKey.OpenBaseKey(hive, view);
            using RegistryKey? uninstallKey = baseKey.OpenSubKey(UninstallSubKey, writable: false);
            if (uninstallKey is null)
                return;

            foreach (string subKeyName in uninstallKey.GetSubKeyNames())
            {
                cancellationToken.ThrowIfCancellationRequested();
                try
                {
                    using RegistryKey? entryKey = uninstallKey.OpenSubKey(subKeyName, writable: false);
                    if (entryKey is null)
                        continue;

                    Dictionary<string, object?> values = new(StringComparer.OrdinalIgnoreCase);
                    foreach (string valueName in ValueNames)
                        values[valueName] = entryKey.GetValue(valueName, null, RegistryValueOptions.DoNotExpandEnvironmentNames);

                    string path = $"{hive}\\{view}\\{UninstallSubKey}\\{subKeyName}";
                    InstalledApplication? application = RegistryEntryMapper.Map(
                        values,
                        path,
                        architecture,
                        requiresElevation);
                    if (application is not null)
                        applications.Add(application);
                }
                catch (Exception exception) when (
                    exception is UnauthorizedAccessException ||
                    exception is IOException ||
                    exception is System.Security.SecurityException)
                {
                    // 单个损坏或不可访问的条目不会中断整个枚举。
                }
            }
        }
        catch (Exception exception) when (
            exception is UnauthorizedAccessException ||
            exception is IOException ||
            exception is System.Security.SecurityException ||
            exception is PlatformNotSupportedException)
        {
            // 不可用的注册表视图会被跳过。
        }
    }
}
