using WindowsToolbox.Core.Commands;
using WindowsToolbox.Core.Utilities;
using WindowsToolbox.Modules.Utilities.Developer.Services;
using WindowsToolbox.Modules.Utilities.Services;

namespace WindowsToolbox.Modules.Utilities.Developer.ViewModels;

/// <summary>
/// Three small offline dev helpers in one page: base conversion, UTF-8 text hash and
/// UUID inspection. Inputs and results live only in this session — nothing is written to
/// disk, logged, or sent anywhere. Hash calculation is explicit (button) so secret-like
/// text is not re-hashed on every keystroke; base and UUID update in real time.
/// </summary>
public sealed class DeveloperToolsViewModel : ObservableObject
{
    private readonly IUtilitiesTextClipboardAdapter _clipboard;

    private int _selectedTabIndex;
    private int _selectedSourceBaseIndex = 2; // Decimal
    private string _baseInputText = "42";
    private string _binaryOutput = string.Empty;
    private string _octalOutput = string.Empty;
    private string _decimalOutput = string.Empty;
    private string _hexOutput = string.Empty;
    private string _baseError = string.Empty;
    private bool _showPrefix;

    private string _hashInputText = string.Empty;
    private int _selectedAlgorithmIndex = 2; // SHA-256
    private string _hashOutput = string.Empty;
    private string _hashError = string.Empty;
    private string _hashStatus = "输入文本后点击“计算 Hash”；输入与结果仅保留在当前会话。";

    private string _uuidInputText = string.Empty;
    private string _uuidValidityText = string.Empty;
    private string _uuidCanonicalD = string.Empty;
    private string _uuidCanonicalN = string.Empty;
    private string _uuidVersion = string.Empty;
    private string _uuidVariant = string.Empty;
    private string _uuidSpecial = string.Empty;
    private string _uuidError = string.Empty;

    private string _copyStatus = string.Empty;

    public DeveloperToolsViewModel()
        : this(new WindowsUtilitiesTextClipboardAdapter()) { }

    public DeveloperToolsViewModel(IUtilitiesTextClipboardAdapter clipboard)
    {
        _clipboard = clipboard ?? throw new ArgumentNullException(nameof(clipboard));
        ComputeHashCommand = new RelayCommand(ComputeHash);
        CopyBaseCommand = new RelayCommand<string>(value => Copy(value, "已复制进制结果。"));
        CopyHashCommand = new RelayCommand<string>(value => Copy(value, "已复制 Hash。"));
        CopyUuidCommand = new RelayCommand<string>(value => Copy(value, "已复制 UUID。"));
        ConvertBase();
        InspectUuid();
    }

    public static IReadOnlyList<string> TabLabels { get; } =
        ["进制转换 / Base Converter", "文本哈希 / Text Hash", "UUID 检查 / UUID Inspector"];

    public static IReadOnlyList<string> SourceBaseLabels { get; } =
        ["Binary · 2", "Octal · 8", "Decimal · 10", "Hexadecimal · 16"];

    public IReadOnlyList<string> AlgorithmLabels => TextHashService.Algorithms;

    public RelayCommand ComputeHashCommand { get; }
    public RelayCommand<string> CopyBaseCommand { get; }
    public RelayCommand<string> CopyHashCommand { get; }
    public RelayCommand<string> CopyUuidCommand { get; }

    public int SelectedTabIndex
    {
        get => _selectedTabIndex;
        set => SetProperty(ref _selectedTabIndex, value);
    }

    public string CopyStatus { get => _copyStatus; private set => SetProperty(ref _copyStatus, value); }

    // ---- Base Converter ----

    public IReadOnlyList<string> SourceBases => SourceBaseLabels;

    public int SelectedSourceBaseIndex
    {
        get => _selectedSourceBaseIndex;
        set
        {
            if (value < 0 || value >= NumberBaseConverter.SupportedBases.Count)
                return;
            if (SetProperty(ref _selectedSourceBaseIndex, value))
                ConvertBase();
        }
    }

    public int SelectedSourceBase => NumberBaseConverter.SupportedBases[SelectedSourceBaseIndex];

    public string BaseInputText
    {
        get => _baseInputText;
        set { if (SetProperty(ref _baseInputText, value ?? string.Empty)) ConvertBase(); }
    }

    public bool ShowPrefix
    {
        get => _showPrefix;
        set { if (SetProperty(ref _showPrefix, value)) ConvertBase(); }
    }

    public string BinaryOutput { get => _binaryOutput; private set => SetProperty(ref _binaryOutput, value); }
    public string OctalOutput { get => _octalOutput; private set => SetProperty(ref _octalOutput, value); }
    public string DecimalOutput { get => _decimalOutput; private set => SetProperty(ref _decimalOutput, value); }
    public string HexOutput { get => _hexOutput; private set => SetProperty(ref _hexOutput, value); }
    public string BaseError { get => _baseError; private set => SetProperty(ref _baseError, value); }

    private void ConvertBase()
    {
        OnPropertyChanged(nameof(SelectedSourceBase));
        if (string.IsNullOrWhiteSpace(BaseInputText))
        {
            ClearBaseOutputs();
            return;
        }

        if (!NumberBaseConverter.TryConvert(BaseInputText, SelectedSourceBase, ShowPrefix,
                out BaseConversionResult result, out string? error))
        {
            ClearBaseOutputs();
            BaseError = error ?? "转换失败。";
            return;
        }

        BaseError = string.Empty;
        BinaryOutput = result.Binary;
        OctalOutput = result.Octal;
        DecimalOutput = result.DecimalValue;
        HexOutput = result.Hex;
    }

    private void ClearBaseOutputs()
    {
        BaseError = string.Empty;
        BinaryOutput = string.Empty;
        OctalOutput = string.Empty;
        DecimalOutput = string.Empty;
        HexOutput = string.Empty;
    }

    // ---- Text Hash ----

    public string HashInputText
    {
        get => _hashInputText;
        set
        {
            if (!SetProperty(ref _hashInputText, value ?? string.Empty))
                return;
            MarkHashOutdated();
        }
    }

    public int SelectedAlgorithmIndex
    {
        get => _selectedAlgorithmIndex;
        set
        {
            if (value < 0 || value >= TextHashService.Algorithms.Count)
                return;
            if (SetProperty(ref _selectedAlgorithmIndex, value))
            {
                OnPropertyChanged(nameof(CurrentAlgorithm));
                OnPropertyChanged(nameof(AlgorithmNoteText));
                MarkHashOutdated();
            }
        }
    }

    public string CurrentAlgorithm => TextHashService.Algorithms[SelectedAlgorithmIndex];
    public string AlgorithmNoteText => TextHashService.AlgorithmNote(CurrentAlgorithm);

    public string HashOutput { get => _hashOutput; private set => SetProperty(ref _hashOutput, value); }
    public string HashError { get => _hashError; private set => SetProperty(ref _hashError, value); }
    public string HashStatus { get => _hashStatus; private set => SetProperty(ref _hashStatus, value); }

    private void ComputeHash()
    {
        HashError = string.Empty;
        if (!TextHashService.TryCompute(HashInputText, CurrentAlgorithm, out string hash, out string? error))
        {
            HashError = error ?? "计算失败。";
            HashOutput = string.Empty;
            HashStatus = "计算失败，未生成结果。";
            return;
        }

        HashOutput = hash;
        HashStatus = TextHashService.IsSoftLimitExceeded(HashInputText)
            ? "文本较大（> 1 MiB），计算完成；输入与结果仅保留在当前会话。"
            : "计算完成 · UTF-8 · 输入与结果仅保留在当前会话。";
    }

    private void MarkHashOutdated()
    {
        if (!string.IsNullOrEmpty(HashOutput))
            HashStatus = "输入或算法已更改，结果待重新计算。";
    }

    // ---- UUID Inspector ----

    public string UuidInputText
    {
        get => _uuidInputText;
        set { if (SetProperty(ref _uuidInputText, value ?? string.Empty)) InspectUuid(); }
    }

    public string UuidValidityText { get => _uuidValidityText; private set => SetProperty(ref _uuidValidityText, value); }
    public string UuidCanonicalD { get => _uuidCanonicalD; private set => SetProperty(ref _uuidCanonicalD, value); }
    public string UuidCanonicalN { get => _uuidCanonicalN; private set => SetProperty(ref _uuidCanonicalN, value); }
    public string UuidVersion { get => _uuidVersion; private set => SetProperty(ref _uuidVersion, value); }
    public string UuidVariant { get => _uuidVariant; private set => SetProperty(ref _uuidVariant, value); }
    public string UuidSpecial { get => _uuidSpecial; private set => SetProperty(ref _uuidSpecial, value); }
    public string UuidError { get => _uuidError; private set => SetProperty(ref _uuidError, value); }
    public bool IsUuidValid => !string.IsNullOrEmpty(UuidCanonicalD);

    private void InspectUuid()
    {
        if (string.IsNullOrWhiteSpace(UuidInputText))
        {
            ResetUuidDisplay(string.Empty);
            return;
        }

        UuidInspection result = UuidInspector.Inspect(UuidInputText);
        if (!result.IsValid)
        {
            ResetUuidDisplay(result.Error ?? "UUID 格式无效。");
            return;
        }

        UuidError = string.Empty;
        UuidValidityText = "有效 / Valid";
        UuidCanonicalD = result.CanonicalD ?? string.Empty;
        UuidCanonicalN = result.CanonicalN ?? string.Empty;
        UuidVersion = result.Version ?? "Unknown";
        UuidVariant = result.Variant ?? "Unknown";
        UuidSpecial = result.SpecialName is null ? string.Empty : $"特殊：{result.SpecialName}";
        OnPropertyChanged(nameof(IsUuidValid));
    }

    private void ResetUuidDisplay(string error)
    {
        UuidValidityText = string.Empty;
        UuidCanonicalD = string.Empty;
        UuidCanonicalN = string.Empty;
        UuidVersion = string.Empty;
        UuidVariant = string.Empty;
        UuidSpecial = string.Empty;
        UuidError = error;
        OnPropertyChanged(nameof(IsUuidValid));
    }

    private void Copy(string? value, string status)
    {
        if (string.IsNullOrEmpty(value))
            return;
        try
        {
            _clipboard.SetText(value);
            CopyStatus = status;
        }
        catch
        {
            CopyStatus = "无法写入剪贴板，请稍后重试。";
        }
    }
}
