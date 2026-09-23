using System.Buffers.Binary;
using WindowsToolbox.Modules.Utilities.Random.Models;
using WindowsToolbox.Modules.Utilities.Random.Services;
using WindowsToolbox.Modules.Utilities.Random.ViewModels;
using WindowsToolbox.Modules.Utilities.Services;

namespace WindowsToolbox.Tests;

[TestClass]
public sealed class RandomToolsViewModelTests
{
    [TestMethod]
    public async Task RandomViewModelGeneratesEachModeOnlyOnRequestAndCopiesPlainText()
    {
        FakeClipboard clipboard = new();
        using RandomToolsViewModel viewModel = CreateViewModel(new FakeRandomSource(), clipboard);

        Assert.AreEqual(RandomGenerationType.Uuid, viewModel.SelectedType);
        viewModel.SelectedTypeIndex = -1;
        Assert.AreEqual(RandomGenerationType.Uuid, viewModel.SelectedType);
        Assert.AreEqual("1", viewModel.CountText);
        Assert.AreEqual("16", viewModel.StringLengthText);
        Assert.IsTrue(viewModel.Uppercase && viewModel.Lowercase && viewModel.Digits);
        Assert.IsFalse(viewModel.Symbols);
        Assert.AreEqual(0, viewModel.Results.Count);

        await viewModel.GenerateAsync();
        Assert.AreEqual(1, viewModel.Results.Count);
        Assert.AreEqual(36, viewModel.Results[0].Length);
        Assert.AreEqual('4', viewModel.Results[0][14]);

        viewModel.SelectedType = RandomGenerationType.String;
        viewModel.CountText = "3";
        viewModel.StringLengthText = "4";
        viewModel.Uppercase = true;
        viewModel.Lowercase = false;
        viewModel.Digits = false;
        await viewModel.GenerateAsync();
        CollectionAssert.AreEqual(new[] { "AAAA", "AAAA", "AAAA" }, viewModel.Results.ToArray());

        viewModel.CopyResultCommand.Execute(viewModel.Results[1]);
        Assert.AreEqual("AAAA", clipboard.Text);
        viewModel.CopyAllCommand.Execute(null);
        Assert.AreEqual(string.Join(Environment.NewLine, viewModel.Results), clipboard.Text);

        viewModel.SelectedType = RandomGenerationType.Integer;
        viewModel.MinimumText = "-2";
        viewModel.MaximumText = "2";
        await viewModel.GenerateAsync();
        CollectionAssert.AreEqual(new[] { "-2", "-2", "-2" }, viewModel.Results.ToArray());
    }

    [TestMethod]
    public async Task InvalidValuesAndOversizedOutputShowTextErrorsWithoutReplacingResults()
    {
        using RandomToolsViewModel viewModel = CreateViewModel(new FakeRandomSource());
        await viewModel.GenerateAsync();
        string previous = viewModel.Results.Single();

        viewModel.CountText = "1001";
        await viewModel.GenerateAsync();
        Assert.AreEqual("请检查数量、长度和整数范围限制。", viewModel.Error);
        Assert.AreEqual(previous, viewModel.Results.Single());

        viewModel.CountText = "one";
        await viewModel.GenerateAsync();
        Assert.AreEqual("数量、长度和整数范围必须是有效的十进制整数。", viewModel.Error);

        viewModel.SelectedType = RandomGenerationType.String;
        viewModel.CountText = "1000";
        viewModel.StringLengthText = "1048";
        await viewModel.GenerateAsync();
        Assert.AreEqual("请检查数量、长度和整数范围限制。", viewModel.Error);

        viewModel.StringLengthText = "16";
        viewModel.Uppercase = false;
        viewModel.Lowercase = false;
        viewModel.Digits = false;
        viewModel.Symbols = false;
        viewModel.CountText = "1";
        await viewModel.GenerateAsync();
        Assert.AreEqual("请检查字符集和参数设置。", viewModel.Error);
    }

    [TestMethod]
    public async Task ClearingDuringGenerationDropsLateOutputAndDisposeClearsSessionResults()
    {
        using ManualResetEventSlim started = new();
        using ManualResetEventSlim release = new();
        FakeRandomSource random = new(started, release);
        RandomToolsViewModel viewModel = CreateViewModel(random);
        viewModel.SelectedType = RandomGenerationType.String;
        viewModel.StringLengthText = "1";

        Task pending = viewModel.GenerateAsync();
        Assert.IsTrue(await Task.Run(() => started.Wait(TimeSpan.FromSeconds(3))));
        viewModel.ClearCommand.Execute(null);
        release.Set();
        await pending;

        Assert.AreEqual(0, viewModel.Results.Count);
        Assert.AreEqual("结果已清除。", viewModel.Status);
        await viewModel.GenerateAsync();
        Assert.AreEqual(1, viewModel.Results.Count);
        viewModel.Dispose();
        Assert.AreEqual(0, viewModel.Results.Count);
    }

    private static RandomToolsViewModel CreateViewModel(FakeRandomSource random, FakeClipboard? clipboard = null) =>
        new(new RandomToolsService(random), clipboard ?? new FakeClipboard());

    private sealed class FakeRandomSource(ManualResetEventSlim? started = null, ManualResetEventSlim? release = null) : ISecureRandomSource
    {
        public int GetInt32(int maxExclusive)
        {
            started?.Set();
            release?.Wait(TimeSpan.FromSeconds(5));
            return 0;
        }

        public void Fill(Span<byte> buffer) => BinaryPrimitives.WriteUInt32LittleEndian(buffer, 0);
    }

    private sealed class FakeClipboard : IUtilitiesTextClipboardAdapter
    {
        public string? Text { get; private set; }
        public void SetText(string text) => Text = text;
    }
}
