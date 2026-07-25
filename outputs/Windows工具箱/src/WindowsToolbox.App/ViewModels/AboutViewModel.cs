using System.Reflection;

namespace WindowsToolbox.App.ViewModels;

public sealed class AboutViewModel
{
    public string Version { get; } = GetVersion();
    public string Runtime => ".NET 8 · WPF";

    private static string GetVersion()
    {
        Assembly assembly = Assembly.GetEntryAssembly() ?? typeof(AboutViewModel).Assembly;
        string? informationalVersion = assembly
            .GetCustomAttribute<AssemblyInformationalVersionAttribute>()
            ?.InformationalVersion;
        if (!string.IsNullOrWhiteSpace(informationalVersion))
            return informationalVersion.Split('+')[0];

        Version? version = assembly.GetName().Version;
        return version is null
            ? "1.1.1"
            : $"{version.Major}.{version.Minor}.{version.Build}";
    }
}
