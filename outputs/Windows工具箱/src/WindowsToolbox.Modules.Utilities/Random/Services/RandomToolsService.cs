using System.Buffers.Binary;
using System.Text;
using WindowsToolbox.Modules.Utilities.Random.Models;

namespace WindowsToolbox.Modules.Utilities.Random.Services;

public sealed class RandomToolsService(ISecureRandomSource random)
{
    private readonly ISecureRandomSource _random = random ?? throw new ArgumentNullException(nameof(random));
    public const int MaximumCount = 1000;
    public const int MaximumLength = 4096;
    public const int MaximumOutputCharacters = 1024 * 1024;
    public const string SymbolCharacters = "!@#$%^&*()-_=+[]{};:,.?";
    private const string UppercaseCharacters = "ABCDEFGHIJKLMNOPQRSTUVWXYZ";
    private const string LowercaseCharacters = "abcdefghijklmnopqrstuvwxyz";
    private const string DigitCharacters = "0123456789";
    private const ulong UInt32Space = 1UL << 32;

    public string GenerateUuid(UuidFormat format = UuidFormat.Standard, bool uppercase = false)
    {
        if (!Enum.IsDefined(format)) throw new ArgumentOutOfRangeException(nameof(format));
        string value = Guid.NewGuid().ToString(format == UuidFormat.Standard ? "D" : "N");
        return uppercase ? value.ToUpperInvariant() : value.ToLowerInvariant();
    }

    public IReadOnlyList<string> GenerateUuids(int count, UuidFormat format = UuidFormat.Standard, bool uppercase = false)
    {
        ValidateCount(count);
        string[] values = new string[count];
        for (int i = 0; i < values.Length; i++) values[i] = GenerateUuid(format, uppercase);
        return Array.AsReadOnly(values);
    }

    public IReadOnlyList<string> GenerateStrings(int count, int length, RandomStringOptions options)
    {
        ArgumentNullException.ThrowIfNull(options);
        ValidateCount(count);
        if (length is < 1 or > MaximumLength) throw new ArgumentOutOfRangeException(nameof(length));
        if ((long)count * length + count - 1 > MaximumOutputCharacters)
            throw new ArgumentOutOfRangeException(nameof(count), "Combined random-string output exceeds the 1 MiB limit.");

        StringBuilder charset = new(UppercaseCharacters.Length + LowercaseCharacters.Length + DigitCharacters.Length + SymbolCharacters.Length);
        if (options.Uppercase) charset.Append(UppercaseCharacters);
        if (options.Lowercase) charset.Append(LowercaseCharacters);
        if (options.Digits) charset.Append(DigitCharacters);
        if (options.Symbols) charset.Append(SymbolCharacters);
        if (charset.Length == 0) throw new ArgumentException("Select at least one character set.", nameof(options));

        string characters = charset.ToString();
        string[] values = new string[count];
        for (int line = 0; line < count; line++)
        {
            char[] value = new char[length];
            for (int index = 0; index < value.Length; index++)
                value[index] = characters[_random.GetInt32(characters.Length)];
            values[line] = new string(value);
        }
        return Array.AsReadOnly(values);
    }

    public IReadOnlyList<int> GenerateIntegers(int minimum, int maximum, int count)
    {
        ValidateCount(count);
        if (minimum > maximum) throw new ArgumentOutOfRangeException(nameof(minimum), "Minimum must not exceed maximum.");
        int[] values = new int[count];
        for (int i = 0; i < values.Length; i++) values[i] = GenerateInteger(minimum, maximum);
        return Array.AsReadOnly(values);
    }

    private int GenerateInteger(int minimum, int maximum)
    {
        if (minimum == maximum) return minimum;

        ulong range = (ulong)((long)maximum - minimum + 1);
        ulong acceptedLimit = UInt32Space - UInt32Space % range;
        Span<byte> bytes = stackalloc byte[sizeof(uint)];
        while (true)
        {
            _random.Fill(bytes);
            ulong sample = BinaryPrimitives.ReadUInt32LittleEndian(bytes);
            if (sample >= acceptedLimit) continue;
            return checked((int)(minimum + (long)(sample % range)));
        }
    }

    private static void ValidateCount(int count)
    {
        if (count is < 1 or > MaximumCount) throw new ArgumentOutOfRangeException(nameof(count));
    }
}
