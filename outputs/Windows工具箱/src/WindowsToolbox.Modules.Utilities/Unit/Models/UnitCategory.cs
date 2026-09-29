namespace WindowsToolbox.Modules.Utilities.Unit.Models;

/// <summary>A physical quantity group (length, mass, …) with a fixed base unit and sensible defaults.</summary>
public sealed record UnitCategory(
    string Id,
    string ChineseName,
    string EnglishName,
    string BaseUnitId,
    string DefaultFromUnitId,
    string DefaultToUnitId,
    IReadOnlyList<UnitDefinition> Units)
{
    public UnitDefinition? Find(string unitId) =>
        Units.FirstOrDefault(unit => string.Equals(unit.Id, unitId, StringComparison.Ordinal));

    public string DisplayLabel => $"{ChineseName} / {EnglishName}";
}
