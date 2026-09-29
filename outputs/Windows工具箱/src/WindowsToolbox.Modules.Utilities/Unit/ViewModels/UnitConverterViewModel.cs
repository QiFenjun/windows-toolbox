using WindowsToolbox.Core.Commands;
using WindowsToolbox.Core.Utilities;
using WindowsToolbox.Modules.Utilities.Services;
using WindowsToolbox.Modules.Utilities.Unit.Models;
using WindowsToolbox.Modules.Utilities.Unit.Services;

namespace WindowsToolbox.Modules.Utilities.Unit.ViewModels;

/// <summary>
/// Offline unit-conversion UI state. Everything is computed synchronously on the UI thread
/// (decimal math is instant); no timers, no background work, no persistence.
/// </summary>
public sealed class UnitConverterViewModel : ObservableObject
{
    private readonly UnitConversionService _service = new();
    private readonly IUtilitiesTextClipboardAdapter _clipboard;
    private string _inputText = "1";
    private string _resultText = string.Empty;
    private string _error = string.Empty;
    private string _copyStatus = string.Empty;
    private int _selectedCategoryIndex;
    private UnitDefinition? _selectedFromUnit;
    private UnitDefinition? _selectedToUnit;
    private IReadOnlyList<UnitDefinition> _fromUnits = [];
    private IReadOnlyList<UnitDefinition> _toUnits = [];

    public UnitConverterViewModel()
        : this(new WindowsUtilitiesTextClipboardAdapter()) { }

    public UnitConverterViewModel(IUtilitiesTextClipboardAdapter clipboard)
    {
        _clipboard = clipboard ?? throw new ArgumentNullException(nameof(clipboard));
        SwapCommand = new RelayCommand(Swap);
        CopyValueCommand = new RelayCommand(() => Copy(includeUnit: false));
        CopyWithUnitCommand = new RelayCommand(() => Copy(includeUnit: true));
        LoadCategory(UnitCatalog.Categories[0]);
    }

    public static IReadOnlyList<UnitCategory> Categories => UnitCatalog.Categories;

    public IReadOnlyList<string> CategoryLabels { get; } =
        UnitCatalog.Categories.Select(category => category.DisplayLabel).ToArray();

    public IReadOnlyList<UnitDefinition> FromUnits { get => _fromUnits; private set => SetProperty(ref _fromUnits, value); }
    public IReadOnlyList<UnitDefinition> ToUnits { get => _toUnits; private set => SetProperty(ref _toUnits, value); }

    public RelayCommand SwapCommand { get; }
    public RelayCommand CopyValueCommand { get; }
    public RelayCommand CopyWithUnitCommand { get; }

    public int SelectedCategoryIndex
    {
        get => _selectedCategoryIndex;
        set
        {
            if (value < 0 || value >= Categories.Count)
                return;
            if (!SetProperty(ref _selectedCategoryIndex, value))
                return;
            LoadCategory(Categories[value]);
            Convert();
        }
    }

    public UnitDefinition? SelectedFromUnit
    {
        get => _selectedFromUnit;
        set { if (SetProperty(ref _selectedFromUnit, value)) Convert(); }
    }

    public UnitDefinition? SelectedToUnit
    {
        get => _selectedToUnit;
        set { if (SetProperty(ref _selectedToUnit, value)) Convert(); }
    }

    public string InputText
    {
        get => _inputText;
        set { if (SetProperty(ref _inputText, value ?? string.Empty)) Convert(); }
    }

    public string ResultText { get => _resultText; private set => SetProperty(ref _resultText, value); }
    public string Error { get => _error; private set => SetProperty(ref _error, value); }
    public string CopyStatus { get => _copyStatus; private set => SetProperty(ref _copyStatus, value); }
    public bool HasError => !string.IsNullOrEmpty(Error);

    private void LoadCategory(UnitCategory category)
    {
        FromUnits = category.Units;
        ToUnits = category.Units;
        _selectedFromUnit = category.Find(category.DefaultFromUnitId);
        _selectedToUnit = category.Find(category.DefaultToUnitId);
        OnPropertyChanged(nameof(SelectedFromUnit));
        OnPropertyChanged(nameof(SelectedToUnit));
    }

    private void Swap()
    {
        if (SelectedFromUnit is null || SelectedToUnit is null)
            return;
        UnitDefinition from = SelectedFromUnit;
        _selectedFromUnit = SelectedToUnit;
        _selectedToUnit = from;
        OnPropertyChanged(nameof(SelectedFromUnit));
        OnPropertyChanged(nameof(SelectedToUnit));
        Convert();
    }

    private void Convert()
    {
        if (SelectedFromUnit is null || SelectedToUnit is null)
            return;

        UnitCategory category = Categories[_selectedCategoryIndex];
        ConversionResult result = _service.Convert(InputText, category.Id, SelectedFromUnit.Id, SelectedToUnit.Id);
        ResultText = result.Success ? result.Value : string.Empty;
        Error = result.Success ? string.Empty : result.Error ?? string.Empty;
        OnPropertyChanged(nameof(HasError));
        CopyStatus = string.Empty;
    }

    private void Copy(bool includeUnit)
    {
        if (string.IsNullOrEmpty(ResultText) || SelectedToUnit is null)
            return;
        try
        {
            string text = includeUnit ? $"{ResultText} {SelectedToUnit.Symbol}" : ResultText;
            _clipboard.SetText(text);
            CopyStatus = includeUnit ? "已复制数值与单位。" : "已复制数值。";
        }
        catch
        {
            CopyStatus = "无法写入剪贴板，请稍后重试。";
        }
    }
}
