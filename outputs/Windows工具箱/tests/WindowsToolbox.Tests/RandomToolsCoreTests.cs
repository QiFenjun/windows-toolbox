using System.Buffers.Binary;
using WindowsToolbox.Modules.Utilities.Random.Models;
using WindowsToolbox.Modules.Utilities.Random.Services;

namespace WindowsToolbox.Tests;

[TestClass]
public sealed class RandomToolsCoreTests
{
    [TestMethod]
    public void UuidV4SupportsStandardCompactCaseAndBatchFormats()
    {
        RandomToolsService service = new(new FakeRandomSource());
        IReadOnlyList<string> values = service.GenerateUuids(100);

        Assert.AreEqual(100, values.Count);
        Assert.AreEqual(100, values.Distinct(StringComparer.Ordinal).Count());
        foreach (string value in values)
        {
            Assert.AreEqual(value, value.ToLowerInvariant());
            Assert.AreEqual(36, value.Length);
            Assert.AreEqual('4', value[14]);
            Assert.IsTrue("89ab".Contains(value[19]));
            Assert.IsTrue(Guid.TryParseExact(value, "D", out _));
        }

        string compact = service.GenerateUuid(UuidFormat.Compact, uppercase: true);
        Assert.AreEqual(32, compact.Length);
        Assert.AreEqual(compact, compact.ToUpperInvariant());
        Assert.IsTrue(Guid.TryParseExact(compact, "N", out Guid compactGuid));
        string normalized = compactGuid.ToString("D");
        Assert.AreEqual('4', normalized[14]);
        Assert.IsTrue("89ab".Contains(normalized[19]));
    }

    [TestMethod]
    public void UuidBatchRejectsCountsOutsideTheBound()
    {
        RandomToolsService service = new(new FakeRandomSource());
        Assert.AreEqual(RandomToolsService.MaximumCount, service.GenerateUuids(RandomToolsService.MaximumCount).Count);
        Assert.ThrowsException<ArgumentOutOfRangeException>(() => service.GenerateUuids(0));
        Assert.ThrowsException<ArgumentOutOfRangeException>(() => service.GenerateUuids(-1));
        Assert.ThrowsException<ArgumentOutOfRangeException>(() => service.GenerateUuids(1001));
        Assert.ThrowsException<ArgumentOutOfRangeException>(() => service.GenerateUuid((UuidFormat)99));
    }

    [TestMethod]
    public void StringsUseSelectedCharacterSetsAndUniformBoundedIndexApi()
    {
        FakeRandomSource defaults = new(choices: [0, 26, 52]);
        RandomToolsService service = new(defaults);
        Assert.AreEqual("Aa0", service.GenerateStrings(1, 3, new RandomStringOptions()).Single());
        CollectionAssert.AreEqual(new[] { 62, 62, 62 }, defaults.GetInt32Bounds.ToArray());

        Assert.AreEqual("ZZZZ", new RandomToolsService(new FakeRandomSource(choices: [25, 25, 25, 25]))
            .GenerateStrings(1, 4, new(Uppercase: true, Lowercase: false, Digits: false)).Single());
        Assert.AreEqual("zzzz", new RandomToolsService(new FakeRandomSource(choices: [25, 25, 25, 25]))
            .GenerateStrings(1, 4, new(Uppercase: false, Lowercase: true, Digits: false)).Single());
        Assert.AreEqual("9999", new RandomToolsService(new FakeRandomSource(choices: [9, 9, 9, 9]))
            .GenerateStrings(1, 4, new(Uppercase: false, Lowercase: false, Digits: true)).Single());
        int lastSymbol = RandomToolsService.SymbolCharacters.Length - 1;
        Assert.AreEqual("????", new RandomToolsService(new FakeRandomSource(choices: [lastSymbol, lastSymbol, lastSymbol, lastSymbol]))
            .GenerateStrings(1, 4, new(false, false, false, true)).Single());
        string combined = service.GenerateStrings(1, 1024, new(Symbols: true)).Single();
        Assert.IsTrue(combined.All(character => char.IsAsciiLetterOrDigit(character) || RandomToolsService.SymbolCharacters.Contains(character)));
    }

    [TestMethod]
    public void StringsEnforceLengthCountCharsetAndCombinedOutputLimits()
    {
        RandomToolsService service = new(new FakeRandomSource());
        Assert.AreEqual("A", service.GenerateStrings(1, 1, new(Uppercase: true, Lowercase: false, Digits: false)).Single());
        Assert.AreEqual(4096, service.GenerateStrings(1, RandomToolsService.MaximumLength, new()).Single().Length);
        Assert.AreEqual(1000, service.GenerateStrings(RandomToolsService.MaximumCount, 1, new()).Count);
        Assert.AreEqual(1000, service.GenerateStrings(1000, 1023, new()).Count);
        Assert.ThrowsException<ArgumentOutOfRangeException>(() => service.GenerateStrings(1, 0, new()));
        Assert.ThrowsException<ArgumentOutOfRangeException>(() => service.GenerateStrings(1, 4097, new()));
        Assert.ThrowsException<ArgumentOutOfRangeException>(() => service.GenerateStrings(1, int.MaxValue, new()));
        Assert.ThrowsException<ArgumentOutOfRangeException>(() => service.GenerateStrings(0, 16, new()));
        Assert.ThrowsException<ArgumentOutOfRangeException>(() => service.GenerateStrings(1001, 1, new()));
        Assert.ThrowsException<ArgumentOutOfRangeException>(() => service.GenerateStrings(1000, 1048, new()));
        Assert.ThrowsException<ArgumentException>(() => service.GenerateStrings(1, 16, new(false, false, false, false)));
    }

    [TestMethod]
    public void IntegersAreInclusiveSupportNegativeValuesAndAllowRepeats()
    {
        FakeRandomSource random = new(samples: [0, 8, 4, 0, 4]);
        RandomToolsService service = new(random);
        CollectionAssert.AreEqual(new[] { -4, 4, 0 }, service.GenerateIntegers(-4, 4, 3).ToArray());
        CollectionAssert.AreEqual(new[] { -8, -4 }, service.GenerateIntegers(-8, -4, 2).ToArray());
        CollectionAssert.AreEqual(new[] { 7, 7 }, service.GenerateIntegers(7, 7, 2).ToArray());
        Assert.AreEqual(5, random.FillCount);
        Assert.ThrowsException<ArgumentOutOfRangeException>(() => service.GenerateIntegers(2, 1, 1));
        Assert.ThrowsException<ArgumentOutOfRangeException>(() => service.GenerateIntegers(1, 2, 0));
        Assert.ThrowsException<ArgumentOutOfRangeException>(() => service.GenerateIntegers(1, 2, 1001));
    }

    [TestMethod]
    public void IntegerGeneratorIncludesIntMaxWithoutOverflowAndRejectsBiasedTail()
    {
        FakeRandomSource random = new(samples: [uint.MaxValue, int.MaxValue - 1, 0, uint.MaxValue]);
        RandomToolsService service = new(random);
        CollectionAssert.AreEqual(new[] { int.MaxValue, 1 }, service.GenerateIntegers(1, int.MaxValue, 2).ToArray());
        Assert.AreEqual(3, random.FillCount);

        Assert.AreEqual(int.MaxValue,
            service.GenerateIntegers(int.MinValue, int.MaxValue, 1).Single());
        Assert.AreEqual(4, random.FillCount);
    }

    private sealed class FakeRandomSource(IEnumerable<int>? choices = null, IEnumerable<uint>? samples = null) : ISecureRandomSource
    {
        private readonly Queue<int> _choices = new(choices ?? []);
        private readonly Queue<uint> _samples = new(samples ?? []);
        public List<int> GetInt32Bounds { get; } = [];
        public int FillCount { get; private set; }

        public int GetInt32(int maxExclusive)
        {
            GetInt32Bounds.Add(maxExclusive);
            int value = _choices.Count == 0 ? 0 : _choices.Dequeue();
            if (value < 0 || value >= maxExclusive) throw new InvalidOperationException("Fake value was outside the requested range.");
            return value;
        }

        public void Fill(Span<byte> buffer)
        {
            FillCount++;
            uint value = _samples.Count == 0 ? 0 : _samples.Dequeue();
            if (buffer.Length != sizeof(uint)) throw new InvalidOperationException("Unexpected random byte request size.");
            BinaryPrimitives.WriteUInt32LittleEndian(buffer, value);
        }
    }
}
