namespace WindowsToolbox.Modules.Utilities.Models;

/// <summary>Static metadata for the small set of tools hosted by Utilities.</summary>
public sealed record UtilityToolDescriptor(
    string Id,
    string ChineseName,
    string EnglishName,
    string IconKey,
    string Description);
