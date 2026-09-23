using System.Collections.ObjectModel;
using System.Globalization;
using System.Security.Cryptography;
using WindowsToolbox.Core.Commands;
using WindowsToolbox.Core.Utilities;
using WindowsToolbox.Modules.Utilities.Random.Models;
using WindowsToolbox.Modules.Utilities.Random.Services;
using WindowsToolbox.Modules.Utilities.Services;

namespace WindowsToolbox.Modules.Utilities.Random.ViewModels;

public sealed class RandomToolsViewModel : ObservableObject, IDisposable
{
    public static IReadOnlyList<string> GenerationTypes { get; } = Array.AsReadOnly(new[] { "UUID v4", "随机字符串", "随机整数" });
    public static IReadOnlyList<string> UuidFormatLabels { get; } = Array.AsReadOnly(new[] { "标准 · 带连字符", "紧凑 · 无连字符" });
    private readonly RandomToolsService _service;
    private readonly IUtilitiesTextClipboardAdapter _clipboard;
    private string _countText = "1";
    private string _stringLengthText = "16";
    private string _minimumText = "1";
    private string _maximumText = "100";
    private string _error = string.Empty;
    private string _status = "结果只保留在当前会话中。";
    private RandomGenerationType _selectedType = RandomGenerationType.Uuid;
    private UuidFormat _uuidFormat = UuidFormat.Standard;
    private bool _uppercaseUuid;
    private bool _uppercase = true;
    private bool _lowercase = true;
    private bool _digits = true;
    private bool _symbols;
    private bool _isGenerating;
    private bool _disposed;
    private int _generationId;

    public RandomToolsViewModel()
        : this(new RandomToolsService(new SystemSecureRandomSource()), new WindowsUtilitiesTextClipboardAdapter()) { }

    public RandomToolsViewModel(RandomToolsService service, IUtilitiesTextClipboardAdapter clipboard)
    {
        _service = service ?? throw new ArgumentNullException(nameof(service));
        _clipboard = clipboard ?? throw new ArgumentNullException(nameof(clipboard));
        GenerateCommand = new RelayCommand(() => _ = GenerateAsync(), () => !IsGenerating && !_disposed);
        ClearCommand = new RelayCommand(Clear, () => Results.Count > 0 || IsGenerating);
        CopyResultCommand = new RelayCommand<string>(CopyResult, value => !string.IsNullOrEmpty(value));
        CopyAllCommand = new RelayCommand(CopyAll, () => Results.Count > 0);
    }

    public ObservableCollection<string> Results { get; } = [];
    public RelayCommand GenerateCommand { get; }
    public RelayCommand ClearCommand { get; }
    public RelayCommand<string> CopyResultCommand { get; }
    public RelayCommand CopyAllCommand { get; }
    public IReadOnlyList<string> AvailableGenerationTypes => GenerationTypes;
    public IReadOnlyList<string> AvailableUuidFormats => UuidFormatLabels;

    public RandomGenerationType SelectedType
    {
        get => _selectedType;
        set
        {
            if (!Enum.IsDefined(value)) throw new ArgumentOutOfRangeException(nameof(value));
            if (SetProperty(ref _selectedType, value))
            {
                OnPropertyChanged(nameof(IsUuidMode));
                OnPropertyChanged(nameof(IsStringMode));
                OnPropertyChanged(nameof(IsIntegerMode));
            }
        }
    }
    public bool IsUuidMode => SelectedType == RandomGenerationType.Uuid;
    public bool IsStringMode => SelectedType == RandomGenerationType.String;
    public bool IsIntegerMode => SelectedType == RandomGenerationType.Integer;
    public int SelectedTypeIndex
    {
        get => (int)SelectedType;
        set
        {
            if (value == -1) return;
            SelectedType = Enum.IsDefined((RandomGenerationType)value) ? (RandomGenerationType)value : throw new ArgumentOutOfRangeException(nameof(value));
        }
    }

    public string CountText { get => _countText; set => SetProperty(ref _countText, value ?? string.Empty); }
    public string StringLengthText { get => _stringLengthText; set => SetProperty(ref _stringLengthText, value ?? string.Empty); }
    public string MinimumText { get => _minimumText; set => SetProperty(ref _minimumText, value ?? string.Empty); }
    public string MaximumText { get => _maximumText; set => SetProperty(ref _maximumText, value ?? string.Empty); }
    public string Error { get => _error; private set => SetProperty(ref _error, value); }
    public string Status { get => _status; private set => SetProperty(ref _status, value); }
    public bool IsGenerating
    {
        get => _isGenerating;
        private set
        {
            if (SetProperty(ref _isGenerating, value))
            {
                GenerateCommand.NotifyCanExecuteChanged();
                ClearCommand.NotifyCanExecuteChanged();
            }
        }
    }
    public UuidFormat SelectedUuidFormat
    {
        get => _uuidFormat;
        set
        {
            if (!Enum.IsDefined(value)) throw new ArgumentOutOfRangeException(nameof(value));
            SetProperty(ref _uuidFormat, value);
        }
    }
    public int SelectedUuidFormatIndex
    {
        get => (int)SelectedUuidFormat;
        set
        {
            if (value == -1) return;
            SelectedUuidFormat = Enum.IsDefined((UuidFormat)value) ? (UuidFormat)value : throw new ArgumentOutOfRangeException(nameof(value));
        }
    }
    public bool UppercaseUuid { get => _uppercaseUuid; set => SetProperty(ref _uppercaseUuid, value); }
    public bool Uppercase { get => _uppercase; set => SetProperty(ref _uppercase, value); }
    public bool Lowercase { get => _lowercase; set => SetProperty(ref _lowercase, value); }
    public bool Digits { get => _digits; set => SetProperty(ref _digits, value); }
    public bool Symbols { get => _symbols; set => SetProperty(ref _symbols, value); }

    public async Task GenerateAsync()
    {
        if (_disposed || IsGenerating) return;
        int generation = Interlocked.Increment(ref _generationId);
        IsGenerating = true;
        Error = string.Empty;
        Status = "正在生成…";
        try
        {
            if (!int.TryParse(CountText, NumberStyles.Integer, CultureInfo.InvariantCulture, out int count))
                throw new FormatException();

            RandomGenerationType type = SelectedType;
            UuidFormat format = SelectedUuidFormat;
            bool uppercaseUuid = UppercaseUuid;
            int length = 0;
            int minimum = 0;
            int maximum = 0;
            RandomStringOptions options = new(Uppercase, Lowercase, Digits, Symbols);
            if (type == RandomGenerationType.String &&
                !int.TryParse(StringLengthText, NumberStyles.Integer, CultureInfo.InvariantCulture, out length))
                throw new FormatException();
            if (type == RandomGenerationType.Integer &&
                (!int.TryParse(MinimumText, NumberStyles.Integer, CultureInfo.InvariantCulture, out minimum) ||
                 !int.TryParse(MaximumText, NumberStyles.Integer, CultureInfo.InvariantCulture, out maximum)))
                throw new FormatException();

            string[] values = await Task.Run(() => type switch
            {
                RandomGenerationType.Uuid => _service.GenerateUuids(count, format, uppercaseUuid).ToArray(),
                RandomGenerationType.String => _service.GenerateStrings(count, length, options).ToArray(),
                RandomGenerationType.Integer => _service.GenerateIntegers(minimum, maximum, count)
                    .Select(value => value.ToString(CultureInfo.InvariantCulture)).ToArray(),
                _ => throw new ArgumentOutOfRangeException(nameof(type))
            }).ConfigureAwait(true);

            if (_disposed || generation != _generationId) return;
            Results.Clear();
            foreach (string value in values) Results.Add(value);
            Status = $"已生成 {Results.Count} 项 · 仅当前会话";
            NotifyResultCommands();
        }
        catch (FormatException)
        {
            if (!_disposed && generation == _generationId)
            {
                Error = "数量、长度和整数范围必须是有效的十进制整数。";
                Status = "生成失败，保留上次结果。";
            }
        }
        catch (ArgumentOutOfRangeException)
        {
            if (!_disposed && generation == _generationId)
            {
                Error = "请检查数量、长度和整数范围限制。";
                Status = "生成失败，保留上次结果。";
            }
        }
        catch (ArgumentException)
        {
            if (!_disposed && generation == _generationId)
            {
                Error = "请检查字符集和参数设置。";
                Status = "生成失败，保留上次结果。";
            }
        }
        catch (CryptographicException)
        {
            if (!_disposed && generation == _generationId)
            {
                Error = "系统安全随机源不可用，未生成结果。";
                Status = "生成失败，保留上次结果。";
            }
        }
        catch
        {
            if (!_disposed && generation == _generationId)
            {
                Error = "随机数据生成失败，未替换现有结果。";
                Status = "生成失败，保留上次结果。";
            }
        }
        finally
        {
            if (!_disposed && generation == _generationId) IsGenerating = false;
        }
    }

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;
        Interlocked.Increment(ref _generationId);
        IsGenerating = false;
        Results.Clear();
        NotifyResultCommands();
    }

    private void Clear()
    {
        Interlocked.Increment(ref _generationId);
        IsGenerating = false;
        Results.Clear();
        Error = string.Empty;
        Status = "结果已清除。";
        NotifyResultCommands();
    }

    private void CopyResult(string? value)
    {
        if (string.IsNullOrEmpty(value)) return;
        try { _clipboard.SetText(value); Status = "已复制所选结果。"; }
        catch { Error = "无法写入剪贴板，请稍后重试。"; }
    }

    private void CopyAll()
    {
        if (Results.Count == 0) return;
        try { _clipboard.SetText(string.Join(Environment.NewLine, Results)); Status = "已复制全部结果。"; }
        catch { Error = "无法写入剪贴板，请稍后重试。"; }
    }

    private void NotifyResultCommands()
    {
        CopyAllCommand.NotifyCanExecuteChanged();
        ClearCommand.NotifyCanExecuteChanged();
    }
}
