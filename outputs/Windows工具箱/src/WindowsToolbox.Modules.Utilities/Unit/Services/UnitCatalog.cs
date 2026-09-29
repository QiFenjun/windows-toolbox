using WindowsToolbox.Modules.Utilities.Unit.Models;

namespace WindowsToolbox.Modules.Utilities.Unit.Services;

/// <summary>
/// Single source of truth for every unit and conversion factor.
/// Sources are noted per category; linear factors express "1 unit = factor base units".
/// Temperature units use <see cref="UnitKind.Temperature"/> and are converted by
/// <see cref="UnitConversionService"/> with dedicated affine formulas.
/// Time units are intentionally absent: Time Tools owns time conversions.
/// Currency is intentionally absent: it would require online rates.
/// </summary>
public static class UnitCatalog
{
    // π to 28 significant digits (NIST). Used so angle conversion stays in decimal arithmetic.
    private const decimal PiDegreesPerRadian = 57.295779513082320876798154814m;

    private static readonly IReadOnlyList<UnitCategory> CategoriesInner =
    [
        new UnitCategory("length", "长度", "Length", "m", "m", "km",
        [
            new("nm", "纳米", "nm", 0.000000001m),
            new("um", "微米", "μm", 0.000001m),
            new("mm", "毫米", "mm", 0.001m),
            new("cm", "厘米", "cm", 0.01m),
            new("m", "米", "m", 1m),
            new("km", "千米", "km", 1000m),
            // International yard and pound (1959): 1 in = 2.54 cm, 1 ft = 0.3048 m,
            // 1 yd = 0.9144 m, 1 mi = 1609.344 m — all exact by definition.
            new("in", "英寸", "in", 0.0254m),
            new("ft", "英尺", "ft", 0.3048m),
            new("yd", "码", "yd", 0.9144m),
            new("mi", "英里", "mi", 1609.344m)
        ]),
        new UnitCategory("mass", "质量", "Mass", "g", "kg", "g",
        [
            new("mg", "毫克", "mg", 0.001m),
            new("g", "克", "g", 1m),
            new("kg", "千克", "kg", 1000m),
            new("t", "吨", "t", 1000000m),
            // Avoirdupois ounce/pound, exact by definition: 1 oz = 28.349523125 g, 1 lb = 453.59237 g.
            new("oz", "盎司", "oz", 28.349523125m),
            new("lb", "磅", "lb", 453.59237m)
        ]),
        new UnitCategory("temperature", "温度", "Temperature", "c", "c", "f",
        [
            new("c", "摄氏度", "°C", 1m, UnitKind.Temperature),
            new("f", "华氏度", "°F", 1m, UnitKind.Temperature),
            new("k", "开尔文", "K", 1m, UnitKind.Temperature)
        ]),
        new UnitCategory("area", "面积", "Area", "m2", "m2", "cm2",
        [
            new("mm2", "平方毫米", "mm²", 0.000001m),
            new("cm2", "平方厘米", "cm²", 0.0001m),
            new("m2", "平方米", "m²", 1m),
            new("km2", "平方千米", "km²", 1000000m),
            // Area factors are the exact squares of the length definitions above.
            new("in2", "平方英寸", "in²", 0.00064516m),
            new("ft2", "平方英尺", "ft²", 0.09290304m),
            new("ha", "公顷", "ha", 10000m),          // 1 ha = 10 000 m² (SI)
            new("ac", "英亩", "ac", 4046.8564224m)    // 1 acre = 4840 yd² = 4046.8564224 m² (exact)
        ]),
        new UnitCategory("volume", "体积", "Volume", "ml", "l", "ml",
        [
            new("ml", "毫升", "mL", 1m),
            new("l", "升", "L", 1000m),
            new("cm3", "立方厘米", "cm³", 1m),          // 1 cm³ = 1 mL exactly
            new("m3", "立方米", "m³", 1000000m),        // 1 m³ = 1000 L exactly
            new("in3", "立方英寸", "in³", 16.387064m),  // 2.54³ cm³, exact
            new("ft3", "立方英尺", "ft³", 28316.846592m), // 1728 in³, exact
            new("usgal", "美制加仑", "US gal", 3785.411784m) // 1 US gal = 231 in³, exact (not imperial gallon)
        ]),
        new UnitCategory("speed", "速度", "Speed", "ms", "ms", "kmh",
        [
            new("ms", "米/秒", "m/s", 1m),
            // 1 km/h = 1000/3600 m/s = 0.2777… m/s (repeating, stored to 28 significant digits).
            new("kmh", "千米/小时", "km/h", 0.2777777777777777777777777778m),
            new("mph", "英里/小时", "mph", 0.44704m),   // 1609.344/3600 m/s, exact
            // 1 knot = 1 international nautical mile per hour = 1852/3600 m/s (repeating).
            new("kn", "节", "kn", 0.5144444444444444444444444444m)
        ]),
        new UnitCategory("pressure", "压力", "Pressure", "pa", "kpa", "mpa",
        [
            new("pa", "帕斯卡", "Pa", 1m),
            new("kpa", "千帕", "kPa", 1000m),
            new("mpa", "兆帕", "MPa", 1000000m),
            new("bar", "巴", "bar", 100000m),          // 1 bar = 100 000 Pa (exactly)
            new("atm", "标准大气压", "atm", 101325m),   // 1 atm = 101 325 Pa (exactly)
            // 1 psi = 1 lbf/in² = 4.4482216152605 N / (0.0254 m)² ≈ 6894.757293168361344506536189 Pa.
            new("psi", "磅力/平方英寸", "psi", 6894.757293168361344506536189m)
        ]),
        new UnitCategory("energy", "能量", "Energy", "j", "j", "kj",
        [
            new("j", "焦耳", "J", 1m),
            new("kj", "千焦", "kJ", 1000m),
            new("wh", "瓦时", "Wh", 3600m),            // 1 W·h = 3600 J (exactly)
            new("kwh", "千瓦时", "kWh", 3600000m),
            // Thermochemical calorie: 1 cal = 4.184 J (exactly). Not the 15 °C calorie.
            new("cal", "卡路里", "cal", 4.184m),
            new("kcal", "千卡", "kcal", 4184m)
        ]),
        new UnitCategory("power", "功率", "Power", "w", "w", "kw",
        [
            new("w", "瓦", "W", 1m),
            new("kw", "千瓦", "kW", 1000m),
            new("mw", "兆瓦", "MW", 1000000m)
            // No horsepower in v1.11.0: mechanical/metric/ electrical hp differ and would be ambiguous.
        ]),
        new UnitCategory("angle", "角度", "Angle", "deg", "deg", "rad",
        [
            new("deg", "度", "°", 1m),
            new("rad", "弧度", "rad", PiDegreesPerRadian), // 1 rad = 180/π degrees
            new("turn", "圈", "turn", 360m)
        ]),
        new UnitCategory("data", "数据大小", "Data Size", "B", "MB", "MiB",
        [
            new("bit", "位", "bit", 0.125m),           // 1 bit = 1/8 byte (exact)
            new("B", "字节", "B", 1m),
            // SI (decimal): 1 KB = 1000 B, powers of 1000.
            new("KB", "千字节 (SI)", "KB", 1000m),
            new("MB", "兆字节 (SI)", "MB", 1000000m),
            new("GB", "吉字节 (SI)", "GB", 1000000000m),
            new("TB", "太字节 (SI)", "TB", 1000000000000m),
            // IEC (binary): 1 KiB = 1024 B, powers of 1024. KB ≠ KiB, MB ≠ MiB.
            new("KiB", "千字节 (IEC)", "KiB", 1024m),
            new("MiB", "兆字节 (IEC)", "MiB", 1048576m),
            new("GiB", "吉字节 (IEC)", "GiB", 1073741824m),
            new("TiB", "太字节 (IEC)", "TiB", 1099511627776m)
        ])
    ];

    public static IReadOnlyList<UnitCategory> Categories => CategoriesInner;

    public static UnitCategory? Find(string? categoryId) =>
        CategoriesInner.FirstOrDefault(category =>
            string.Equals(category.Id, categoryId, StringComparison.Ordinal));
}
