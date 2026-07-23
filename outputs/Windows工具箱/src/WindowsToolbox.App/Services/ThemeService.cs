using Microsoft.Win32;
using System.Windows;
using WindowsToolbox.Core.Interfaces;
using WindowsToolbox.Core.Models;

namespace WindowsToolbox.App.Services;

public sealed class ThemeService : IThemeService
{
    public ThemeMode CurrentMode { get; private set; } = ThemeMode.System;

    public void Apply(ThemeMode mode)
    {
        CurrentMode = mode;
        ThemeMode effectiveMode = mode == ThemeMode.System ? GetSystemTheme() : mode;
        string dictionaryName = effectiveMode == ThemeMode.Dark
            ? "Colors.Dark.xaml"
            : "Colors.Light.xaml";

        var dictionaries = Application.Current.Resources.MergedDictionaries;
        ResourceDictionary? existing = dictionaries.FirstOrDefault(dictionary =>
            dictionary.Source?.OriginalString.Contains("Colors.Light.xaml", StringComparison.OrdinalIgnoreCase) == true ||
            dictionary.Source?.OriginalString.Contains("Colors.Dark.xaml", StringComparison.OrdinalIgnoreCase) == true);

        ResourceDictionary replacement = new()
        {
            Source = new Uri($"Themes/{dictionaryName}", UriKind.Relative)
        };

        if (existing is null)
            dictionaries.Insert(2, replacement);
        else
        {
            int index = dictionaries.IndexOf(existing);
            dictionaries[index] = replacement;
        }
    }

    private static ThemeMode GetSystemTheme()
    {
        try
        {
            object? value = Registry.GetValue(
                @"HKEY_CURRENT_USER\Software\Microsoft\Windows\CurrentVersion\Themes\Personalize",
                "AppsUseLightTheme",
                1);
            return value is int intValue && intValue == 0 ? ThemeMode.Dark : ThemeMode.Light;
        }
        catch
        {
            return ThemeMode.Light;
        }
    }
}
