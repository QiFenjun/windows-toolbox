using System.Windows;
using WindowsToolbox.Core.Interfaces;

namespace WindowsToolbox.App.Services;

public sealed class LocalizationService
{
    private static readonly IReadOnlyDictionary<string, string> EnglishDescriptions = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
    {
        ["shutdown"] = "Schedule shutdown, restart, sign-out, or sleep.",
        ["installed-apps"] = "Review installed apps and safely open their uninstallers.",
        ["clipboard-plus"] = "Store and search encrypted local clipboard history.",
        ["text-tools"] = "Transform, compare, encode, and inspect text.",
        ["file-tools"] = "Batch rename, hash, and inspect local files.",
        ["quick-launch"] = "Open favorite apps, folders, files, and websites.",
        ["window-tools"] = "Inspect, position, resize, or keep windows on top.",
        ["lock-inspector"] = "Find which processes are using a file.",
        ["keep-awake"] = "Temporarily keep your PC or display awake.",
        ["utilities"] = "QR, color, time, random, unit, image, and regex utilities.",
        ["network-traffic"] = "Inspect local per-app network traffic and connections."
    };

    private bool _initialized;
    public const string Chinese = "zh-CN";
    public const string English = "en-US";
    public string CurrentLanguage { get; private set; } = Chinese;
    public bool IsEnglish => CurrentLanguage == English;
    public event EventHandler? LanguageChanged;

    public void Apply(string? language)
    {
        string next = string.Equals(language, English, StringComparison.OrdinalIgnoreCase) ? English : Chinese;
        if (_initialized && CurrentLanguage == next) return;

        ResourceDictionary? current = Application.Current?.Resources.MergedDictionaries.FirstOrDefault(dictionary =>
            dictionary.Source?.OriginalString.Contains("Strings.", StringComparison.OrdinalIgnoreCase) == true);
        if (Application.Current is not null)
        {
            ResourceDictionary replacement = new()
            {
                Source = new Uri($"/Windows工具箱;component/Themes/Strings.{next}.xaml", UriKind.Relative)
            };
            if (current is null) Application.Current.Resources.MergedDictionaries.Insert(0, replacement);
            else Application.Current.Resources.MergedDictionaries[Application.Current.Resources.MergedDictionaries.IndexOf(current)] = replacement;
        }

        bool changed = CurrentLanguage != next;
        CurrentLanguage = next;
        _initialized = true;
        if (changed) LanguageChanged?.Invoke(this, EventArgs.Empty);
    }

    public string GetString(string key) =>
        Application.Current?.TryFindResource(key) as string ?? key;

    public string GetModuleDescription(IToolModule module) => IsEnglish
        ? EnglishDescriptions.GetValueOrDefault(module.Id, module.EnglishName)
        : module.Description;
}
