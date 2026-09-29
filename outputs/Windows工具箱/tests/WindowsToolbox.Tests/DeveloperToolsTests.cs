using System.Numerics;
using System.Reflection;
using WindowsToolbox.Modules.Utilities.Developer.Services;
using WindowsToolbox.Modules.Utilities.Developer.ViewModels;
using WindowsToolbox.Modules.Utilities.Models;
using WindowsToolbox.Modules.Utilities.Random.ViewModels;
using WindowsToolbox.Modules.Utilities.Services;
using WindowsToolbox.Modules.Utilities.ViewModels;

namespace WindowsToolbox.Tests;

[TestClass]
public sealed class DeveloperToolsTests
{
    private static (bool Ok, BaseConversionResult Result, string? Error) Convert(string? input, int baseValue, bool showPrefix = false)
    {
        bool ok = NumberBaseConverter.TryConvert(input, baseValue, showPrefix, out BaseConversionResult result, out string? error);
        return (ok, result, error);
    }

    // ---- Base Converter: 59–73 ----

    [TestMethod]
    public void Base_Binary101010IsDecimal42()
    {
        (bool ok, BaseConversionResult result, _) = Convert("101010", NumberBaseConverter.BinaryBase);
        Assert.IsTrue(ok);
        Assert.AreEqual("42", result.DecimalValue);
        Assert.AreEqual("101010", result.Binary);
        Assert.AreEqual("52", result.Octal);
        Assert.AreEqual("2A", result.Hex);
    }

    [TestMethod]
    public void Base_Octal52IsDecimal42()
    {
        (bool ok, BaseConversionResult result, _) = Convert("52", NumberBaseConverter.OctalBase);
        Assert.IsTrue(ok);
        Assert.AreEqual("42", result.DecimalValue);
        Assert.AreEqual("2A", result.Hex);
    }

    [TestMethod]
    public void Base_Decimal42Is42()
    {
        (bool ok, BaseConversionResult result, _) = Convert("42", NumberBaseConverter.DecimalBase);
        Assert.IsTrue(ok);
        Assert.AreEqual("42", result.DecimalValue);
        Assert.AreEqual("101010", result.Binary);
    }

    [TestMethod]
    public void Base_Hex2AIsDecimal42()
    {
        (bool ok, BaseConversionResult result, _) = Convert("2A", NumberBaseConverter.HexBase);
        Assert.IsTrue(ok);
        Assert.AreEqual("42", result.DecimalValue);
    }

    [TestMethod]
    public void Base_HexOutputIsUppercase()
    {
        (bool ok, BaseConversionResult result, _) = Convert("deadbeef", NumberBaseConverter.HexBase);
        Assert.IsTrue(ok);
        Assert.AreEqual("DEADBEEF", result.Hex);
        Assert.AreEqual("3735928559", result.DecimalValue);
    }

    [TestMethod]
    public void Base_NegativeNumbersUseMathematicalSign()
    {
        (bool ok, BaseConversionResult result, _) = Convert("-42", NumberBaseConverter.DecimalBase);
        Assert.IsTrue(ok);
        Assert.AreEqual("-101010", result.Binary);
        Assert.AreEqual("-2A", result.Hex);

        (bool okBack, BaseConversionResult back, _) = Convert("-101010", NumberBaseConverter.BinaryBase);
        Assert.IsTrue(okBack);
        Assert.AreEqual("-42", back.DecimalValue);
    }

    [TestMethod]
    public void Base_BigIntegerBeyondInt64()
    {
        // 2^64 + 1 = 18446744073709551617
        (bool ok, BaseConversionResult result, _) = Convert("18446744073709551617", NumberBaseConverter.DecimalBase);
        Assert.IsTrue(ok);
        Assert.IsTrue(BigInteger.Parse(result.DecimalValue) > long.MaxValue);
        Assert.AreEqual("10000000000000001", result.Hex);
        Assert.AreEqual(65, result.Binary.Length);
    }

    [TestMethod]
    public void Base_InvalidBinaryDigitFailsWholesale()
    {
        (bool ok, _, string? error) = Convert("10201", NumberBaseConverter.BinaryBase);
        Assert.IsFalse(ok);
        Assert.IsNotNull(error);
    }

    [TestMethod]
    public void Base_InvalidOctalDigitFailsWholesale()
        => Assert.IsFalse(Convert("89", NumberBaseConverter.OctalBase).Ok);

    [TestMethod]
    public void Base_InvalidDecimalFailsWholesale()
        => Assert.IsFalse(Convert("12a4", NumberBaseConverter.DecimalBase).Ok);

    [TestMethod]
    public void Base_InvalidHexFailsWholesale()
        => Assert.IsFalse(Convert("XYZ", NumberBaseConverter.HexBase).Ok);

    [TestMethod]
    public void Base_MatchingPrefixIsAccepted()
    {
        Assert.AreEqual("10", Convert("0b1010", NumberBaseConverter.BinaryBase).Result.DecimalValue);
        Assert.AreEqual("42", Convert("0o52", NumberBaseConverter.OctalBase).Result.DecimalValue);
        Assert.AreEqual("42", Convert("0x2A", NumberBaseConverter.HexBase).Result.DecimalValue);
        Assert.AreEqual("42", Convert("0B101010", NumberBaseConverter.BinaryBase).Result.DecimalValue);
    }

    [TestMethod]
    public void Base_ConflictingPrefixIsRejectedWithoutSilentSwitch()
    {
        (bool ok, _, string? error) = Convert("0xFF", NumberBaseConverter.BinaryBase);
        Assert.IsFalse(ok);
        Assert.AreEqual("前缀与当前进制不一致。", error);

        // Prefix on a base that has no prefixes (decimal) must also be rejected:
        Assert.IsFalse(Convert("0xFF", NumberBaseConverter.DecimalBase).Ok);
        Assert.IsFalse(Convert("0b1010", NumberBaseConverter.DecimalBase).Ok);
    }

    [TestMethod]
    public void Base_MaxInputLimitIsEnforced()
    {
        string huge = new('1', NumberBaseConverter.MaxInputLength + 1);
        (bool ok, _, string? error) = Convert(huge, NumberBaseConverter.BinaryBase);
        Assert.IsFalse(ok);
        Assert.AreEqual("输入过长。", error);

        (bool okAtLimit, _, _) = Convert(new string('1', NumberBaseConverter.MaxInputLength), NumberBaseConverter.BinaryBase);
        Assert.IsTrue(okAtLimit);
    }

    [TestMethod]
    public void Base_ZeroIsZeroInEveryBase()
    {
        (bool ok, BaseConversionResult result, _) = Convert("0", NumberBaseConverter.DecimalBase);
        Assert.IsTrue(ok);
        Assert.AreEqual("0", result.Binary);
        Assert.AreEqual("0", result.Octal);
        Assert.AreEqual("0", result.DecimalValue);
        Assert.AreEqual("0", result.Hex);
    }

    [TestMethod]
    public void Base_ShowPrefixToggleAddsRadixPrefixes()
    {
        (bool ok, BaseConversionResult result, _) = Convert("42", NumberBaseConverter.DecimalBase, showPrefix: true);
        Assert.IsTrue(ok);
        Assert.AreEqual("0b101010", result.Binary);
        Assert.AreEqual("0o52", result.Octal);
        Assert.AreEqual("42", result.DecimalValue);
        Assert.AreEqual("0x2A", result.Hex);
    }

    [TestMethod]
    public void Base_EmptyAndBareSignAreRejectedGracefully()
    {
        Assert.IsFalse(Convert("", NumberBaseConverter.DecimalBase).Ok);
        Assert.IsFalse(Convert("   ", NumberBaseConverter.DecimalBase).Ok);
        Assert.IsFalse(Convert("-", NumberBaseConverter.DecimalBase).Ok);
        Assert.IsFalse(Convert(null, NumberBaseConverter.BinaryBase).Ok);
    }

    // ---- Text Hash: 74–82 (standard published vectors) ----

    [TestMethod] public void Hash_EmptyMd5Vector()
    {
        Assert.IsTrue(TextHashService.TryCompute("", "MD5", out string hash, out _));
        Assert.AreEqual("D41D8CD98F00B204E9800998ECF8427E", hash);
    }

    [TestMethod] public void Hash_EmptySha1Vector()
    {
        Assert.IsTrue(TextHashService.TryCompute("", "SHA-1", out string hash, out _));
        Assert.AreEqual("DA39A3EE5E6B4B0D3255BFEF95601890AFD80709", hash);
    }

    [TestMethod] public void Hash_EmptySha256Vector()
    {
        Assert.IsTrue(TextHashService.TryCompute("", "SHA-256", out string hash, out _));
        Assert.AreEqual("E3B0C44298FC1C149AFBF4C8996FB92427AE41E4649B934CA495991B7852B855", hash);
    }

    [TestMethod] public void Hash_EmptySha512Vector()
    {
        Assert.IsTrue(TextHashService.TryCompute("", "SHA-512", out string hash, out _));
        Assert.AreEqual("CF83E1357EEFB8BDF1542850D66D8007D620E4050B5715DC83F4A921D36CE9CE" +
                        "47D0D13C5D85F2B0FF8318D2877EEC2F63B931BD47417A81A538327AF927DA3E", hash);
    }

    [TestMethod] public void Hash_AsciiAbcVector()
    {
        Assert.IsTrue(TextHashService.TryCompute("abc", "SHA-256", out string hash, out _));
        Assert.AreEqual("BA7816BF8F01CFEA414140DE5DAE2223B00361A396177A9CB410FF61F20015AD", hash);
        Assert.IsTrue(TextHashService.TryCompute("abc", "MD5", out string md5, out _));
        Assert.AreEqual("900150983CD24FB0D6963F7D28E17F72", md5);
        Assert.IsTrue(TextHashService.TryCompute("abc", "SHA-1", out string sha1, out _));
        Assert.AreEqual("A9993E364706816ABA3E25717850C26C9CD0D89D", sha1);
        Assert.IsTrue(TextHashService.TryCompute("abc", "SHA-512", out string sha512, out _));
        Assert.AreEqual("DDAF35A193617ABACC417349AE20413112E6FA4E89A97EA20A9EEEE64B55D39A" +
                        "2192992A274FC1A836BA3C23A3FEEBBD454D4423643CE80E2A9AC94FA54CA49F", sha512);
    }

    [TestMethod] public void Hash_ChineseUtf8Vector()
    {
        // Vector from Python hashlib (OpenSSL), UTF-8 encoded — not derived from this implementation.
        Assert.IsTrue(TextHashService.TryCompute("中文", "SHA-256", out string hash, out _));
        Assert.AreEqual("72726D8818F693066CEB69AFA364218B692E62EA92B385782363780F47529C21", hash);
    }

    [TestMethod] public void Hash_EmojiUtf8Vector()
    {
        Assert.IsTrue(TextHashService.TryCompute("😀", "SHA-512", out string hash, out _));
        Assert.AreEqual("9B1CE8B6649E678E1CB7BCA85AFEAAE750ADD5CFB0668D25EBBA5E7F0038F1B6" +
                        "BDCC4BACD909049E752BE2A3A3C0158C0F2BB5A33D8101B2ED5D74A66ECE2425", hash);
    }

    [TestMethod] public void Hash_UnicodeUtf8Vector()
    {
        Assert.IsTrue(TextHashService.TryCompute("héllo wörld", "SHA-256", out string hash, out _));
        Assert.AreEqual("A1003F7D04A4115711D0B48A2EAF1359CE565D2D2A6FD65098DFCFFADEEEF59F", hash);
    }

    [TestMethod] public void Hash_OutputIsDeterministic()
    {
        Assert.IsTrue(TextHashService.TryCompute("stable", "SHA-256", out string first, out _));
        Assert.IsTrue(TextHashService.TryCompute("stable", "SHA-256", out string second, out _));
        Assert.AreEqual(first, second);
    }

    [TestMethod] public void Hash_AlgorithmSwitchChangesDigestLength()
    {
        Assert.IsTrue(TextHashService.TryCompute("abc", "MD5", out string md5, out _));
        Assert.IsTrue(TextHashService.TryCompute("abc", "SHA-1", out string sha1, out _));
        Assert.IsTrue(TextHashService.TryCompute("abc", "SHA-256", out string sha256, out _));
        Assert.IsTrue(TextHashService.TryCompute("abc", "SHA-512", out string sha512, out _));
        Assert.AreEqual(32, md5.Length);
        Assert.AreEqual(40, sha1.Length);
        Assert.AreEqual(64, sha256.Length);
        Assert.AreEqual(128, sha512.Length);
        Assert.AreEqual(4, new[] { md5, sha1, sha256, sha512 }.Distinct().Count());
    }

    [TestMethod] public void Hash_DefaultAlgorithmIsSha256() => Assert.AreEqual("SHA-256", TextHashService.DefaultAlgorithm);

    [TestMethod] public void Hash_OversizedInputIsRejected()
    {
        string huge = new('a', TextHashService.MaxInputBytes + 1);
        Assert.IsFalse(TextHashService.TryCompute(huge, "SHA-256", out _, out string? error));
        StringAssert.Contains(error!, "File Tools");
    }

    [TestMethod] public void Hash_SoftLimitFlagsLargeButAllowedInput()
    {
        Assert.IsFalse(TextHashService.IsSoftLimitExceeded("small"));
        Assert.IsTrue(TextHashService.IsSoftLimitExceeded(new string('a', TextHashService.SoftLimitBytes + 1)));
        Assert.IsTrue(TextHashService.TryCompute(new string('a', TextHashService.SoftLimitBytes + 1), "SHA-256", out _, out _));
    }

    [TestMethod] public void Hash_UnknownAlgorithmFails()
        => Assert.IsFalse(TextHashService.TryCompute("x", "CRC32", out _, out _));

    [TestMethod] public void Hash_CompatibilityAlgorithmsAreLabeled()
    {
        Assert.IsTrue(TextHashService.IsCompatibilityOnly("MD5"));
        Assert.IsTrue(TextHashService.IsCompatibilityOnly("SHA-1"));
        Assert.IsFalse(TextHashService.IsCompatibilityOnly("SHA-256"));
    }

    // ---- UUID Inspector: 83–95 (known vectors) ----

    [TestMethod] public void Uuid_ValidDCanonicalInput()
    {
        UuidInspection result = UuidInspector.Inspect("f47ac10b-58cc-4372-a567-0e02b2c3d479");
        Assert.IsTrue(result.IsValid);
        Assert.AreEqual("f47ac10b-58cc-4372-a567-0e02b2c3d479", result.CanonicalD);
    }

    [TestMethod] public void Uuid_ValidNInputNormalizesToD()
    {
        UuidInspection result = UuidInspector.Inspect("f47ac10b58cc4372a5670e02b2c3d479");
        Assert.IsTrue(result.IsValid);
        Assert.AreEqual("f47ac10b-58cc-4372-a567-0e02b2c3d479", result.CanonicalD);
        Assert.AreEqual("f47ac10b58cc4372a5670e02b2c3d479", result.CanonicalN);
    }

    [TestMethod] public void Uuid_BracesInputIsSupported()
    {
        UuidInspection result = UuidInspector.Inspect("{f47ac10b-58cc-4372-a567-0e02b2c3d479}");
        Assert.IsTrue(result.IsValid);
        Assert.AreEqual("f47ac10b-58cc-4372-a567-0e02b2c3d479", result.CanonicalD);
    }

    [TestMethod] public void Uuid_InvalidInputFails()
    {
        Assert.IsFalse(UuidInspector.Inspect("not-a-uuid").IsValid);
        Assert.IsFalse(UuidInspector.Inspect("").IsValid);
        Assert.IsFalse(UuidInspector.Inspect(null).IsValid);
        Assert.IsFalse(UuidInspector.Inspect("f47ac10b-58cc-4372-a567").IsValid);
        Assert.IsFalse(UuidInspector.Inspect("zzzzac10b-58cc-4372-a567-0e02b2c3d479").IsValid);
        Assert.IsFalse(UuidInspector.Inspect("f47ac10b58cc4372a5670e02b2c3d47").IsValid); // 31 chars
    }

    [TestMethod] public void Uuid_V1KnownVector()
    {
        // RFC 4122 example UUID.
        UuidInspection result = UuidInspector.Inspect("f81d4fae-7dec-11d0-a765-00a0c91e6bf6");
        Assert.IsTrue(result.IsValid);
        Assert.AreEqual("1", result.Version);
        Assert.AreEqual("RFC 4122 / RFC 9562", result.Variant);
    }

    [TestMethod] public void Uuid_V4KnownVector()
    {
        UuidInspection result = UuidInspector.Inspect("f47ac10b-58cc-4372-a567-0e02b2c3d479");
        Assert.IsTrue(result.IsValid);
        Assert.AreEqual("4", result.Version);
        Assert.AreEqual("RFC 4122 / RFC 9562", result.Variant);
    }

    [TestMethod] public void Uuid_V5KnownVector()
    {
        // Wikipedia / RFC example UUID for version 5.
        UuidInspection result = UuidInspector.Inspect("886313e1-3b8a-5372-9b90-0c9aee199e5d");
        Assert.IsTrue(result.IsValid);
        Assert.AreEqual("5", result.Version);
        Assert.AreEqual("RFC 4122 / RFC 9562", result.Variant);
    }

    [TestMethod] public void Uuid_RfcVariantIsDetected()
    {
        // Variant nibble is canonical index 19 (first character of the fourth group).
        foreach (char nibble in new[] { '8', '9', 'a', 'b' })
        {
            UuidInspection result = UuidInspector.Inspect($"00000000-0000-0000-{nibble}000-000000000000");
            Assert.IsTrue(result.IsValid, $"variant nibble {nibble} must parse");
            Assert.AreEqual("RFC 4122 / RFC 9562", result.Variant, $"variant nibble {nibble}");
        }
    }

    [TestMethod] public void Uuid_MicrosoftVariantTestVector()
    {
        UuidInspection result = UuidInspector.Inspect("00000000-0000-0000-c000-000000000000");
        Assert.IsTrue(result.IsValid);
        Assert.AreEqual("Microsoft (Legacy)", result.Variant);
    }

    [TestMethod] public void Uuid_NcsAndFutureVariantsAreLabeled()
    {
        Assert.AreEqual("NCS", UuidInspector.Inspect("00000000-0000-0000-4000-000000000000").Variant);
        Assert.AreEqual("Future Reserved", UuidInspector.Inspect("00000000-0000-0000-f000-000000000000").Variant);
    }

    [TestMethod] public void Uuid_NilUuidIsRecognizedWithoutVersionZero()
    {
        UuidInspection result = UuidInspector.Inspect(UuidInspector.NilUuid);
        Assert.IsTrue(result.IsValid);
        Assert.AreEqual("Nil UUID", result.SpecialName);
        Assert.AreEqual("N/A", result.Version);
        Assert.AreEqual("N/A", result.Variant);
        Assert.AreEqual(UuidInspector.NilUuid, result.CanonicalD);
    }

    [TestMethod] public void Uuid_MaxUuidIsRecognized()
    {
        UuidInspection result = UuidInspector.Inspect(UuidInspector.MaxUuid);
        Assert.IsTrue(result.IsValid);
        Assert.AreEqual("Max UUID", result.SpecialName);
        Assert.AreEqual("Unknown", result.Version);
    }

    [TestMethod] public void Uuid_CanonicalDAndNAreBothProduced()
    {
        UuidInspection result = UuidInspector.Inspect("{F47AC10B-58CC-4372-A567-0E02B2C3D479}");
        Assert.IsTrue(result.IsValid);
        Assert.AreEqual(36, result.CanonicalD!.Length);
        Assert.AreEqual(32, result.CanonicalN!.Length);
        Assert.AreEqual(result.CanonicalD.Replace("-", ""), result.CanonicalN);
    }

    [TestMethod] public void Uuid_VersionExtractionIsNotFooledByGuidByteOrder()
    {
        // .NET Guid stores the first three fields little-endian; reading version from
        // byte[6] style access would be wrong. String-position extraction must be stable
        // across v1/v4/v5 vectors regardless of internal layout.
        (string uuid, string expected)[] vectors =
        [
            ("f81d4fae-7dec-11d0-a765-00a0c91e6bf6", "1"),
            ("f47ac10b-58cc-4372-a567-0e02b2c3d479", "4"),
            ("886313e1-3b8a-5372-9b90-0c9aee199e5d", "5"),
            ("6ba7b810-9dad-11d1-80b4-00c04fd430c8", "1"),
            ("2ed6657d-e927-568b-95e1-2665a8aea6a2", "5"),
            ("123e4567-e89b-12d3-a456-426614174000", "1")
        ];
        foreach ((string uuid, string expected) in vectors)
        {
            UuidInspection result = UuidInspector.Inspect(uuid);
            Assert.IsTrue(result.IsValid, uuid);
            Assert.AreEqual(expected, result.Version, uuid);
            // Version nibble is canonical index 14 — assert directly against the input string.
            Assert.AreEqual(int.Parse(new[] { uuid[14] }, System.Globalization.NumberStyles.HexNumber, null).ToString(),
                result.Version, uuid);
        }
    }

    [TestMethod] public void Uuid_ReservedVersionIsUnknown()
    {
        // Version nibble 0 is not a defined UUID version.
        UuidInspection result = UuidInspector.Inspect("f47ac10b-58cc-0372-a567-0e02b2c3d479");
        Assert.IsTrue(result.IsValid);
        Assert.AreEqual("Unknown", result.Version);
    }

    // ---- Privacy / audit: 96–98 ----

    [TestMethod] public void Privacy_DeveloperServicesArePureAndStatic()
    {
        foreach (Type type in new[] { typeof(NumberBaseConverter), typeof(TextHashService), typeof(UuidInspector) })
        {
            Assert.IsTrue(type.IsAbstract && type.IsSealed, $"{type.Name} must be a static class with no instance state.");
            Assert.AreEqual(0, type.GetMethods(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.DeclaredOnly)
                .Length, $"{type.Name} must not declare instance methods.");
        }
    }

    [TestMethod] public void Privacy_DeveloperServicesExposeNoPersistenceOrLogging()
    {
        foreach (Type type in new[] { typeof(NumberBaseConverter), typeof(TextHashService), typeof(UuidInspector) })
        {
            foreach (MethodInfo method in type.GetMethods(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static))
            {
                Assert.IsFalse(method.Name.Contains("Save", StringComparison.OrdinalIgnoreCase), method.Name);
                Assert.IsFalse(method.Name.Contains("Persist", StringComparison.OrdinalIgnoreCase), method.Name);
                Assert.IsFalse(method.Name.Contains("Log", StringComparison.OrdinalIgnoreCase), method.Name);
            }
        }
    }

    [TestMethod] public void Privacy_UtilitiesAssemblyReferencesNoNetworkStack()
    {
        AssemblyName[] references = typeof(TextHashService).Assembly.GetReferencedAssemblies();
        foreach (AssemblyName reference in references)
        {
            Assert.IsFalse(reference.Name!.StartsWith("System.Net.Http", StringComparison.OrdinalIgnoreCase), reference.Name);
            Assert.IsFalse(reference.Name.StartsWith("System.Net.Requests", StringComparison.OrdinalIgnoreCase), reference.Name);
            Assert.IsFalse(reference.Name.Contains("Logging", StringComparison.OrdinalIgnoreCase), reference.Name);
        }
    }

    // ---- ViewModel UX + copy (fake clipboard) ----

    private sealed class FakeClipboard : IUtilitiesTextClipboardAdapter
    {
        public string? LastText { get; private set; }
        public void SetText(string text) => LastText = text;
    }

    [TestMethod]
    public void DevVm_BaseConverterUpdatesInRealTime()
    {
        DeveloperToolsViewModel viewModel = new(new FakeClipboard());
        viewModel.SelectedSourceBaseIndex = 0; // Binary
        viewModel.BaseInputText = "101010";
        Assert.AreEqual("42", viewModel.DecimalOutput);
        Assert.AreEqual("2A", viewModel.HexOutput);
        Assert.AreEqual(string.Empty, viewModel.BaseError);

        viewModel.SelectedSourceBaseIndex = 3; // Hex
        viewModel.BaseInputText = "FF";
        Assert.AreEqual("255", viewModel.DecimalOutput);
        Assert.AreEqual("11111111", viewModel.BinaryOutput);
    }

    [TestMethod]
    public void DevVm_BaseInvalidInputShowsErrorAndClearsOutputs()
    {
        DeveloperToolsViewModel viewModel = new(new FakeClipboard());
        viewModel.SelectedSourceBaseIndex = 0;
        viewModel.BaseInputText = "10201";
        Assert.AreEqual(string.Empty, viewModel.DecimalOutput);
        Assert.IsFalse(string.IsNullOrEmpty(viewModel.BaseError));
    }

    [TestMethod]
    public void DevVm_HashIsExplicitAndMarksOutdatedOnEdit()
    {
        FakeClipboard clipboard = new();
        DeveloperToolsViewModel viewModel = new(clipboard);
        viewModel.HashInputText = "abc";
        viewModel.ComputeHashCommand.Execute(null);
        Assert.AreEqual("BA7816BF8F01CFEA414140DE5DAE2223B00361A396177A9CB410FF61F20015AD", viewModel.HashOutput);
        string first = viewModel.HashOutput;

        viewModel.HashInputText = "abc2";
        StringAssert.Contains(viewModel.HashStatus, "待重新计算");

        viewModel.ComputeHashCommand.Execute(null);
        Assert.AreNotEqual(first, viewModel.HashOutput, "Recompute must refresh the digest.");
        StringAssert.Contains(viewModel.HashStatus, "计算完成");

        viewModel.CopyHashCommand.Execute(viewModel.HashOutput);
        Assert.AreEqual(viewModel.HashOutput, clipboard.LastText);
    }

    [TestMethod]
    public void DevVm_UuidInspectorUpdatesInRealTime()
    {
        DeveloperToolsViewModel viewModel = new(new FakeClipboard());
        viewModel.UuidInputText = "f47ac10b-58cc-4372-a567-0e02b2c3d479";
        Assert.IsTrue(viewModel.IsUuidValid);
        Assert.AreEqual("4", viewModel.UuidVersion);
        Assert.AreEqual("RFC 4122 / RFC 9562", viewModel.UuidVariant);

        viewModel.UuidInputText = "garbage";
        Assert.IsFalse(viewModel.IsUuidValid);
        Assert.IsFalse(string.IsNullOrEmpty(viewModel.UuidError));

        viewModel.UuidInputText = string.Empty;
        Assert.IsFalse(viewModel.IsUuidValid);
        Assert.AreEqual(string.Empty, viewModel.UuidError, "Clearing the input must clear the error too.");
    }

    [TestMethod]
    public void DevVm_UuidCopyCommandsUsePlainText()
    {
        FakeClipboard clipboard = new();
        DeveloperToolsViewModel viewModel = new(clipboard);
        viewModel.UuidInputText = "F47AC10B58CC4372A5670E02B2C3D479";
        viewModel.CopyUuidCommand.Execute(viewModel.UuidCanonicalD);
        Assert.AreEqual("f47ac10b-58cc-4372-a567-0e02b2c3d479", clipboard.LastText);
        viewModel.CopyUuidCommand.Execute(viewModel.UuidCanonicalN);
        Assert.AreEqual("f47ac10b58cc4372a5670e02b2c3d479", clipboard.LastText);
    }

    [TestMethod]
    public void DevVm_BaseCopyCommandsUsePlainText()
    {
        FakeClipboard clipboard = new();
        DeveloperToolsViewModel viewModel = new(clipboard);
        viewModel.BaseInputText = "42";
        viewModel.CopyBaseCommand.Execute(viewModel.HexOutput);
        Assert.AreEqual("2A", clipboard.LastText);
    }

    // ---- Metadata: 99 ----

    [TestMethod]
    public void Metadata_DeveloperToolsDescriptorMatchesSpec()
    {
        UtilityToolDescriptor descriptor =
            UtilitiesViewModel.Tools.First(tool => tool.Id == "developer-tools");
        Assert.AreEqual("开发者工具", descriptor.ChineseName);
        Assert.AreEqual("Developer Tools", descriptor.EnglishName);
        Assert.AreEqual("进制转换、文本哈希与 UUID 检查", descriptor.Description);
    }

    [TestMethod]
    public void Metadata_DeveloperToolsNavigationInsideUtilities()
    {
        using UtilitiesViewModel viewModel = new();
        viewModel.SelectToolCommand.Execute("developer-tools");
        Assert.IsInstanceOfType(viewModel.SelectedToolContent, typeof(DeveloperToolsViewModel));
        viewModel.SelectToolCommand.Execute("random-tools");
        Assert.IsInstanceOfType(viewModel.SelectedToolContent, typeof(RandomToolsViewModel));
    }

    [TestMethod]
    public void Privacy_DeveloperViewModelHasNoPersistenceOrLoggingMembers()
    {
        foreach (MethodInfo method in typeof(DeveloperToolsViewModel)
                     .GetMethods(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.DeclaredOnly))
        {
            Assert.IsFalse(method.Name.Contains("Save", StringComparison.OrdinalIgnoreCase), method.Name);
            Assert.IsFalse(method.Name.Contains("Persist", StringComparison.OrdinalIgnoreCase), method.Name);
            Assert.IsFalse(method.Name.Contains("Load", StringComparison.OrdinalIgnoreCase), method.Name);
            Assert.IsFalse(method.Name.Contains("Log", StringComparison.OrdinalIgnoreCase), method.Name);
            Assert.IsFalse(method.Name.Contains("File", StringComparison.OrdinalIgnoreCase), method.Name);
        }
    }
}
