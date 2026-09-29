namespace WindowsToolbox.Modules.Utilities.Unit.Models;

/// <summary>How a unit maps onto its category base unit.</summary>
public enum UnitKind
{
    /// <summary>value × ToBaseFactor = base value. Used by every category except temperature.</summary>
    Linear,
    /// <summary>Affine conversion handled by dedicated Celsius-based formulas (°C / °F / K).</summary>
    Temperature
}

/// <summary>One convertible unit. All constants are declared once in <see cref="Services.UnitCatalog"/>.</summary>
public sealed record UnitDefinition(
    string Id,
    string ChineseName,
    string Symbol,
    decimal ToBaseFactor,
    UnitKind Kind = UnitKind.Linear)
{
    /// <summary>Label used by the unit combo boxes, e.g. "米 (m)".</summary>
    public string DisplayLabel => $"{ChineseName} ({Symbol})";
}
