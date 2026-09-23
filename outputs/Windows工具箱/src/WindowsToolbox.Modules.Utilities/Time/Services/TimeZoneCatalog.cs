namespace WindowsToolbox.Modules.Utilities.Time.Services;

public static class TimeZoneCatalog
{
    private static readonly IReadOnlyList<TimeZoneInfo> Zones =
        Array.AsReadOnly(TimeZoneInfo.GetSystemTimeZones().ToArray());

    public static IReadOnlyList<TimeZoneInfo> SystemZones => Zones;

    public static IReadOnlyList<TimeZoneInfo> Search(string? query) =>
        string.IsNullOrWhiteSpace(query)
            ? Zones
            : Zones.Where(zone =>
                    zone.DisplayName.Contains(query, StringComparison.OrdinalIgnoreCase) ||
                    zone.StandardName.Contains(query, StringComparison.OrdinalIgnoreCase) ||
                    zone.Id.Contains(query, StringComparison.OrdinalIgnoreCase))
                .ToArray();
}
