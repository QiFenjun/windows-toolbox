using WindowsToolbox.Modules.Utilities;
using WindowsToolbox.Modules.Utilities.Models;
using WindowsToolbox.Modules.Utilities.Services;
using WindowsToolbox.Modules.Utilities.Unit.Models;
using WindowsToolbox.Modules.Utilities.Unit.Services;
using WindowsToolbox.Modules.Utilities.Unit.ViewModels;
using WindowsToolbox.Modules.Utilities.ViewModels;

namespace WindowsToolbox.Tests;

[TestClass]
public sealed class UnitConverterTests
{
    private readonly UnitConversionService _service = new();

    private string ConvertOk(string input, string category, string from, string to)
    {
        ConversionResult result = _service.Convert(input, category, from, to);
        Assert.IsTrue(result.Success, $"Conversion failed: {result.Error}");
        return result.Value;
    }

    private static decimal ParseOk(string text) => decimal.Parse(text, System.Globalization.CultureInfo.InvariantCulture);

    // ---- Length ----

    [TestMethod] public void Length_MetersToKilometers() => Assert.AreEqual("1", ConvertOk("1000", "length", "m", "km"));
    [TestMethod] public void Length_KilometersToMeters() => Assert.AreEqual("1000", ConvertOk("1", "length", "km", "m"));
    [TestMethod] public void Length_MillimetersToMeters() => Assert.AreEqual("0.001", ConvertOk("1", "length", "mm", "m"));
    [TestMethod] public void Length_MicrometersToNanometers() => Assert.AreEqual("1000", ConvertOk("1", "length", "um", "nm"));
    [TestMethod] public void Length_InchToCentimeters() => Assert.AreEqual("2.54", ConvertOk("1", "length", "in", "cm"));
    [TestMethod] public void Length_FootToMeters() => Assert.AreEqual("0.3048", ConvertOk("1", "length", "ft", "m"));
    [TestMethod] public void Length_MileToKilometers() => Assert.AreEqual("1.609344", ConvertOk("1", "length", "mi", "km"));

    // ---- Mass ----

    [TestMethod] public void Mass_MilligramsToGrams() => Assert.AreEqual("1", ConvertOk("1000", "mass", "mg", "g"));
    [TestMethod] public void Mass_GramsToKilograms() => Assert.AreEqual("1", ConvertOk("1000", "mass", "g", "kg"));
    [TestMethod] public void Mass_KilogramsToPounds() => Assert.AreEqual("1000", ConvertOk("453.59237", "mass", "kg", "lb"));
    [TestMethod] public void Mass_PoundsToKilograms() => Assert.AreEqual("0.45359237", ConvertOk("1", "mass", "lb", "kg"));
    [TestMethod] public void Mass_OuncesToGrams() => Assert.AreEqual("28.349523125", ConvertOk("1", "mass", "oz", "g"));
    [TestMethod] public void Mass_OneKilogramInPoundsReference()
    {
        decimal pounds = ParseOk(ConvertOk("1", "mass", "kg", "lb"));
        Assert.AreEqual(2.2046226218m, Math.Round(pounds, 10), "1 kg ≈ 2.2046226218 lb (reference vector).");
    }

    // ---- Temperature ----

    [TestMethod] public void Temperature_ZeroCelsiusToFahrenheit() => Assert.AreEqual("32", ConvertOk("0", "temperature", "c", "f"));
    [TestMethod] public void Temperature_BoilingCelsiusToFahrenheit() => Assert.AreEqual("212", ConvertOk("100", "temperature", "c", "f"));
    [TestMethod] public void Temperature_FahrenheitToCelsius() => Assert.AreEqual("0", ConvertOk("32", "temperature", "f", "c"));
    [TestMethod] public void Temperature_CelsiusToKelvin() => Assert.AreEqual("273.15", ConvertOk("0", "temperature", "c", "k"));
    [TestMethod] public void Temperature_AbsoluteZeroCelsiusToKelvin() => Assert.AreEqual("0", ConvertOk("-273.15", "temperature", "c", "k"));
    [TestMethod] public void Temperature_AbsoluteZeroKelvinToCelsius() => Assert.AreEqual("-273.15", ConvertOk("0", "temperature", "k", "c"));
    [TestMethod] public void Temperature_BelowAbsoluteZeroIsRejected()
    {
        ConversionResult result = _service.Convert("-300", "temperature", "c", "k");
        Assert.IsFalse(result.Success);
        Assert.AreEqual(UnitConversionService.BelowAbsoluteZeroError, result.Error);
    }
    [TestMethod] public void Temperature_BelowAbsoluteZeroFahrenheitIsRejected()
    {
        ConversionResult result = _service.Convert("-500", "temperature", "f", "c");
        Assert.IsFalse(result.Success);
        Assert.AreEqual(UnitConversionService.BelowAbsoluteZeroError, result.Error);
    }
    [TestMethod] public void Temperature_NegativeKelvinIsRejected()
    {
        ConversionResult result = _service.Convert("-0.0001", "temperature", "k", "c");
        Assert.IsFalse(result.Success);
        Assert.AreEqual(UnitConversionService.BelowAbsoluteZeroError, result.Error);
    }
    [TestMethod] public void Temperature_SubZeroCelsiusIsAllowed() => Assert.AreEqual("255.15", ConvertOk("-18", "temperature", "c", "k"));

    // ---- Area ----

    [TestMethod] public void Area_SquareMetersToSquareCentimeters() => Assert.AreEqual("10000", ConvertOk("1", "area", "m2", "cm2"));
    [TestMethod] public void Area_HectareToSquareMeters() => Assert.AreEqual("10000", ConvertOk("1", "area", "ha", "m2"));
    [TestMethod] public void Area_AcreToSquareMeters() => Assert.AreEqual("4046.8564224", ConvertOk("1", "area", "ac", "m2"));

    // ---- Volume ----

    [TestMethod] public void Volume_MillilitersToLiters() => Assert.AreEqual("1", ConvertOk("1000", "volume", "ml", "l"));
    [TestMethod] public void Volume_LitersToCubicMeters() => Assert.AreEqual("1", ConvertOk("1000", "volume", "l", "m3"));
    [TestMethod] public void Volume_USGallonToMilliliters() => Assert.AreEqual("3785.411784", ConvertOk("1", "volume", "usgal", "ml"));
    [TestMethod] public void Volume_USGallonToLiters() => Assert.AreEqual("3.785411784", ConvertOk("1", "volume", "usgal", "l"));
    [TestMethod] public void Volume_USGallonSymbolIsExplicit()
    {
        UnitDefinition gallon = UnitCatalog.Find("volume")!.Find("usgal")!;
        StringAssert.Contains(gallon.Symbol, "US");
        StringAssert.Contains(gallon.DisplayLabel, "US gal");
    }

    // ---- Speed ----

    [TestMethod] public void Speed_MetersPerSecondToKilometersPerHour() => Assert.AreEqual("3.6", ConvertOk("1", "speed", "ms", "kmh"));
    [TestMethod] public void Speed_KilometersPerHourToMilesPerHour() => Assert.AreEqual("0.621371192237", ConvertOk("1", "speed", "kmh", "mph"));
    [TestMethod] public void Speed_KnotToKilometersPerHour() => Assert.AreEqual("1.852", ConvertOk("1", "speed", "kn", "kmh"));
    [TestMethod] public void Speed_KnotIsNauticalMilesPerHour()
    {
        // 1 knot = 1852 m / 3600 s; factor comment and value are asserted together.
        decimal knot = UnitCatalog.Find("speed")!.Find("kn")!.ToBaseFactor;
        Assert.IsTrue(Math.Abs(knot - 1852m / 3600m) < 1e-27m, $"1 knot must be 1852/3600 m/s, got {knot}.");
    }

    // ---- Pressure ----

    [TestMethod] public void Pressure_PascalsToKilopascals() => Assert.AreEqual("1", ConvertOk("1000", "pressure", "pa", "kpa"));
    [TestMethod] public void Pressure_MegapascalsToPascals() => Assert.AreEqual("1000000", ConvertOk("1", "pressure", "mpa", "pa"));
    [TestMethod] public void Pressure_AtmosphereToPascals() => Assert.AreEqual("101325", ConvertOk("1", "pressure", "atm", "pa"));
    [TestMethod] public void Pressure_BarToKilopascals() => Assert.AreEqual("100", ConvertOk("1", "pressure", "bar", "kpa"));
    [TestMethod] public void Pressure_PsiToKilopascalsReference()
    {
        decimal kpa = ParseOk(ConvertOk("1", "pressure", "psi", "kpa"));
        Assert.IsTrue(Math.Abs(kpa - 6.894757293168361344506536189m) < 1e-9m,
            $"1 psi must be ≈ 6.894757293 kPa, got {kpa}.");
    }
    [TestMethod] public void Pressure_ConcentratedConstantsMatchStandards()
    {
        UnitCategory pressure = UnitCatalog.Find("pressure")!;
        Assert.AreEqual(101325m, pressure.Find("atm")!.ToBaseFactor);
        Assert.AreEqual(100000m, pressure.Find("bar")!.ToBaseFactor);
        Assert.AreEqual(6894.757293168361344506536189m, pressure.Find("psi")!.ToBaseFactor);
    }

    // ---- Energy ----

    [TestMethod] public void Energy_JoulesToKilojoules() => Assert.AreEqual("1", ConvertOk("1000", "energy", "j", "kj"));
    [TestMethod] public void Energy_WattHoursToJoules() => Assert.AreEqual("3600", ConvertOk("1", "energy", "wh", "j"));
    [TestMethod] public void Energy_KilowattHoursToJoules() => Assert.AreEqual("3600000", ConvertOk("1", "energy", "kwh", "j"));
    [TestMethod] public void Energy_CalorieToJoules() => Assert.AreEqual("4.184", ConvertOk("1", "energy", "cal", "j"));
    [TestMethod] public void Energy_KilojoulesFromKilocalories() => Assert.AreEqual("4.184", ConvertOk("1", "energy", "kcal", "kj"));
    [TestMethod] public void Energy_CalorieIsThermochemical()
        => Assert.AreEqual(4.184m, UnitCatalog.Find("energy")!.Find("cal")!.ToBaseFactor);

    // ---- Power ----

    [TestMethod] public void Power_WattsToKilowatts() => Assert.AreEqual("1", ConvertOk("1000", "power", "w", "kw"));
    [TestMethod] public void Power_MegawattsToWatts() => Assert.AreEqual("1000000", ConvertOk("1", "power", "mw", "w"));
    [TestMethod] public void Power_HorsepowerIsIntentionallyAbsent()
        => Assert.IsFalse(UnitCatalog.Find("power")!.Units.Any(unit => unit.Id == "hp"));

    // ---- Angle ----

    [TestMethod] public void Angle_DegreesToRadiansPiVector()
    {
        decimal radians = ParseOk(ConvertOk("180", "angle", "deg", "rad"));
        Assert.IsTrue(Math.Abs(radians - (decimal)Math.PI) < 1e-9m, $"180° must be π rad, got {radians}.");
    }
    [TestMethod] public void Angle_RadiansToDegreesReference()
    {
        decimal degrees = ParseOk(ConvertOk("1", "angle", "rad", "deg"));
        Assert.IsTrue(Math.Abs(degrees - 57.2957795130823208767981548141011m) < 1e-9m,
            $"1 rad must be ≈ 57.2957795130823 deg, got {degrees}.");
    }
    [TestMethod] public void Angle_RadianFactorMatchesHundredEightyOverPi()
    {
        // π to 28 significant digits; (decimal)Math.PI would only carry ~15 digits.
        const decimal pi = 3.1415926535897932384626433833m;
        decimal factor = UnitCatalog.Find("angle")!.Find("rad")!.ToBaseFactor;
        Assert.IsTrue(Math.Abs(factor - 180m / pi) < 1e-24m);
    }
    [TestMethod] public void Angle_TurnToDegrees() => Assert.AreEqual("360", ConvertOk("1", "angle", "turn", "deg"));

    // ---- Data size: SI vs IEC ----

    [TestMethod] public void Data_OneKilobyteIs1000Bytes() => Assert.AreEqual("1000", ConvertOk("1", "data", "KB", "B"));
    [TestMethod] public void Data_OneKibibyteIs1024Bytes() => Assert.AreEqual("1024", ConvertOk("1", "data", "KiB", "B"));
    [TestMethod] public void Data_MBandMiBDiffer()
    {
        Assert.AreEqual("1000000", ConvertOk("1", "data", "MB", "B"));
        Assert.AreEqual("1048576", ConvertOk("1", "data", "MiB", "B"));
    }
    [TestMethod] public void Data_BitsToBytes() => Assert.AreEqual("1", ConvertOk("8", "data", "bit", "B"));
    [TestMethod] public void Data_OneBitIsEighthOfByte() => Assert.AreEqual("0.125", ConvertOk("1", "data", "bit", "B"));
    [TestMethod] public void Data_GibibyteToBytes() => Assert.AreEqual("1073741824", ConvertOk("1", "data", "GiB", "B"));
    [TestMethod] public void Data_TebibyteToBytes() => Assert.AreEqual("1099511627776", ConvertOk("1", "data", "TiB", "B"));

    // ---- Input parsing ----

    [TestMethod] public void Input_NegativeNumber() => Assert.AreEqual("-500", ConvertOk("-5", "length", "m", "cm"));
    [TestMethod] public void Input_DecimalNumber() => Assert.AreEqual("12.7", ConvertOk("0.5", "length", "in", "mm"));
    [TestMethod] public void Input_ScientificNotation() => Assert.AreEqual("1", ConvertOk("1e-6", "length", "m", "um"));
    [TestMethod] public void Input_InvalidNumberFails()
    {
        ConversionResult result = _service.Convert("abc", "length", "m", "km");
        Assert.IsFalse(result.Success);
        Assert.AreEqual(UnitConversionService.InvalidNumberError, result.Error);
    }
    [TestMethod] public void Input_HugeValueFailsGracefully()
    {
        ConversionResult result = _service.Convert("1e30", "length", "m", "km");
        Assert.IsFalse(result.Success);
        Assert.AreEqual(UnitConversionService.InvalidNumberError, result.Error);
    }
    [TestMethod] public void Input_HugeButRepresentableValueUsesScientificNotation()
        => Assert.AreEqual("1E+25", ConvertOk("1e28", "length", "m", "km"));
    [TestMethod] public void Input_Zero() => Assert.AreEqual("0", ConvertOk("0", "length", "m", "km"));
    [TestMethod] public void Input_UnknownCategoryFails()
    {
        ConversionResult result = _service.Convert("1", "currency", "m", "km");
        Assert.IsFalse(result.Success);
        Assert.AreEqual(UnitConversionService.UnknownCategoryError, result.Error);
    }
    [TestMethod] public void Input_UnknownUnitFails()
    {
        ConversionResult result = _service.Convert("1", "length", "parsec", "km");
        Assert.IsFalse(result.Success);
        Assert.AreEqual(UnitConversionService.UnknownUnitError, result.Error);
    }

    // ---- Formatting ----

    [TestMethod] public void Format_StripsMeaninglessTrailingZeros() => Assert.AreEqual("1", UnitConversionService.FormatValue(1.000000000000m));
    [TestMethod] public void Format_ZeroIsZero() => Assert.AreEqual("0", UnitConversionService.FormatValue(0m));
    [TestMethod] public void Format_LargeIntegerStaysExact() => Assert.AreEqual("1099511627776", UnitConversionService.FormatValue(1099511627776m));
    [TestMethod] public void Format_SmallValueUsesPlainScientificNotation() => Assert.AreEqual("1E-9", UnitConversionService.FormatValue(0.000000001m));
    [TestMethod] public void Format_SciNotationTrimsMantissaZeros() => Assert.AreEqual("1.25E-7", UnitConversionService.FormatValue(0.000000125m));
    [TestMethod] public void Format_PositiveExponentCarriesSign() => Assert.AreEqual("1.25E+20", UnitConversionService.FormatValue(125000000000000000000m));
    [TestMethod] public void Format_LongDecimalIsLimitedToTwelveSignificantDigits()
        => Assert.AreEqual("3.14159265359", UnitConversionService.FormatValue(3.1415926535897932384626433833m));
    [TestMethod] public void Format_NegativeValueKeepsSign() => Assert.AreEqual("-0.5", UnitConversionService.FormatValue(-0.5m));
    [TestMethod] public void Format_FixedWindowBoundaryStaysFixed()
    {
        Assert.AreEqual("0.000001", UnitConversionService.FormatValue(0.000001m));
        Assert.AreEqual("1E-7", UnitConversionService.FormatValue(0.0000001m));
    }

    // ---- Catalog metadata ----

    [TestMethod] public void Catalog_ContainsTheElevenFirstVersionCategories()
        => CollectionAssert.AreEqual(
            new[] { "length", "mass", "temperature", "area", "volume", "speed", "pressure", "energy", "power", "angle", "data" },
            UnitCatalog.Categories.Select(category => category.Id).ToArray());

    [TestMethod] public void Catalog_TimeAndCurrencyCategoriesAreIntentionallyAbsent()
    {
        Assert.IsNull(UnitCatalog.Find("time"));
        Assert.IsNull(UnitCatalog.Find("currency"));
    }

    [TestMethod] public void Catalog_DefaultsBelongToTheSelectedCategory()
    {
        foreach (UnitCategory category in UnitCatalog.Categories)
        {
            Assert.IsNotNull(category.Find(category.DefaultFromUnitId), $"{category.Id} default from-unit missing.");
            Assert.IsNotNull(category.Find(category.DefaultToUnitId), $"{category.Id} default to-unit missing.");
            Assert.IsNotNull(category.Find(category.BaseUnitId), $"{category.Id} base unit missing.");
            Assert.AreNotEqual(category.DefaultFromUnitId, category.DefaultToUnitId, $"{category.Id} defaults must differ.");
        }
    }

    [TestMethod] public void Catalog_TemperatureUnitsUseDedicatedKind()
    {
        UnitCategory temperature = UnitCatalog.Find("temperature")!;
        Assert.IsTrue(temperature.Units.All(unit => unit.Kind == UnitKind.Temperature));
        Assert.IsTrue(UnitCatalog.Find("length")!.Units.All(unit => unit.Kind == UnitKind.Linear));
    }

    // ---- UX model: 53–56 (ViewModel with fake clipboard) ----

    private sealed class FakeClipboard : IUtilitiesTextClipboardAdapter
    {
        public string? LastText { get; private set; }
        public void SetText(string text) => LastText = text;
    }

    [TestMethod]
    public void Ux_SwapKeepsInputValueAndRecalculates()
    {
        FakeClipboard clipboard = new();
        UnitConverterViewModel viewModel = new(clipboard);
        viewModel.InputText = "1000";                       // 1000 m → 1 km
        Assert.AreEqual("1", viewModel.ResultText);

        viewModel.SwapCommand.Execute(null);

        Assert.AreEqual("1000", viewModel.InputText, "Swap must keep the input value.");
        Assert.AreEqual("km", viewModel.SelectedFromUnit!.Id);
        Assert.AreEqual("m", viewModel.SelectedToUnit!.Id);
        Assert.AreEqual("1000000", viewModel.ResultText);   // 1000 km → 1 000 000 m
    }

    [TestMethod]
    public void Ux_CategoryChangeSelectsDefaultUnitsAndRecomputes()
    {
        UnitConverterViewModel viewModel = new(new FakeClipboard());
        UnitCategory length = UnitCatalog.Find("length")!;
        viewModel.SelectedFromUnit = length.Find("mi");
        viewModel.SelectedToUnit = length.Find("yd");
        viewModel.InputText = "5";

        viewModel.SelectedCategoryIndex = 2; // temperature

        Assert.AreEqual("temperature", UnitCatalog.Categories[viewModel.SelectedCategoryIndex].Id);
        Assert.AreEqual("c", viewModel.SelectedFromUnit!.Id, "Previous category units must not leak.");
        Assert.AreEqual("f", viewModel.SelectedToUnit!.Id);
        Assert.AreEqual("5", viewModel.InputText, "Input value survives category changes.");
        Assert.AreEqual("41", viewModel.ResultText, "5 °C → 41 °F");
    }

    [TestMethod]
    public void Ux_UnitSelectionTriggersRecalculation()
    {
        UnitConverterViewModel viewModel = new(new FakeClipboard());
        viewModel.InputText = "1000";
        Assert.AreEqual("1", viewModel.ResultText);

        UnitCategory length = UnitCatalog.Find("length")!;
        viewModel.SelectedToUnit = length.Find("cm");
        Assert.AreEqual("100000", viewModel.ResultText); // 1000 m → 100 000 cm
    }

    [TestMethod]
    public void Ux_CopyValueAndCopyValueWithUnitModels()
    {
        FakeClipboard clipboard = new();
        UnitConverterViewModel viewModel = new(clipboard);
        viewModel.InputText = "1000";
        Assert.AreEqual("1", viewModel.ResultText);

        viewModel.CopyValueCommand.Execute(null);
        Assert.AreEqual("1", clipboard.LastText);
        StringAssert.Contains(viewModel.CopyStatus, "数值");

        viewModel.CopyWithUnitCommand.Execute(null);
        Assert.AreEqual("1 km", clipboard.LastText);
        StringAssert.Contains(viewModel.CopyStatus, "单位");
    }

    [TestMethod]
    public void Ux_InvalidInputShowsTextErrorNotJustColor()
    {
        UnitConverterViewModel viewModel = new(new FakeClipboard());
        viewModel.InputText = "abc";
        Assert.AreEqual(string.Empty, viewModel.ResultText);
        Assert.IsTrue(viewModel.HasError);
        Assert.AreEqual(UnitConversionService.InvalidNumberError, viewModel.Error);
    }

    // ---- Metadata: 57–58 ----

    [TestMethod]
    public void Metadata_UnitConverterDescriptorMatchesSpec()
    {
        UtilityToolDescriptor descriptor =
            UtilitiesViewModel.Tools.First(tool => tool.Id == "unit-converter");
        Assert.AreEqual("单位转换", descriptor.ChineseName);
        Assert.AreEqual("Unit Converter", descriptor.EnglishName);
        Assert.AreEqual("长度、质量、温度、压力等常用单位快速转换", descriptor.Description);
    }

    [TestMethod]
    public void Metadata_UtilitiesStaysOneTopLevelModule()
    {
        WindowsToolbox.Core.Services.ModuleRegistry registry = new();
        UtilitiesModule module = new();
        registry.Register(module);
        Assert.AreEqual(1, registry.Modules.Count);
        Assert.AreEqual("utilities", module.Id);

        using UtilitiesViewModel viewModel = new();
        viewModel.SelectToolCommand.Execute("unit-converter");
        Assert.IsInstanceOfType(viewModel.SelectedToolContent, typeof(UnitConverterViewModel));
        Assert.IsTrue(viewModel.SelectedToolContent is not null);
    }
}
