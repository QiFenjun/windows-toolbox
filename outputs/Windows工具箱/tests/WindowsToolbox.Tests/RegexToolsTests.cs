using System.Text.RegularExpressions;
using WindowsToolbox.Modules.Utilities.Regex.Models;
using WindowsToolbox.Modules.Utilities.Regex.Services;
using WindowsToolbox.Modules.Utilities.Regex.ViewModels;
using WindowsToolbox.Modules.Utilities.Services;
using WindowsToolbox.Modules.Utilities.ViewModels;

namespace WindowsToolbox.Tests;

[TestClass]
public sealed class RegexToolsTests
{
    [TestMethod]
    public void RegexDescriptorStaysWithinUtilities() =>
        CollectionAssert.AreEqual(new[] { "qr", "color", "time-tools", "random-tools", "unit-converter", "developer-tools", "image-tools", "regex-tools" },
            UtilitiesViewModel.Tools.Select(tool => tool.Id).ToArray());

    [TestMethod]
    public void LiteralNoMatchMultipleMatchIndicesAndLength()
    {
        RegexResult result = RegexToolsService.Run(new("cat", "a cat and cat", ""));
        Assert.AreEqual(RegexRunStatus.Matches, result.Status);
        CollectionAssert.AreEqual(new[] { 2, 10 }, result.Matches.Select(match => match.Index).ToArray());
        CollectionAssert.AreEqual(new[] { 3, 3 }, result.Matches.Select(match => match.Length).ToArray());
        Assert.AreEqual(RegexRunStatus.NoMatch, RegexToolsService.Run(new("dog", "cat")).Status);
    }

    [TestMethod]
    public void CommonOptionsAreAppliedAndUnsupportedOptionsRejected()
    {
        Assert.AreEqual(RegexRunStatus.Matches, RegexToolsService.Run(new("cat", "CAT", Options: RegexOptions.IgnoreCase)).Status);
        Assert.AreEqual(RegexRunStatus.Matches, RegexToolsService.Run(new("^b", "a\nb", Options: RegexOptions.Multiline)).Status);
        Assert.AreEqual(RegexRunStatus.Matches, RegexToolsService.Run(new("a.b", "a\nb", Options: RegexOptions.Singleline)).Status);
        Assert.AreEqual(RegexRunStatus.Matches, RegexToolsService.Run(new("(?<x>a)(b)", "ab", Options: RegexOptions.ExplicitCapture)).Status);
        Assert.AreEqual(RegexRunStatus.Invalid, RegexToolsService.Run(new("x", "x", Options: RegexOptions.RightToLeft)).Status);
        Assert.AreEqual(RegexRunStatus.Invalid, RegexToolsService.Run(new("x", "x", TimeoutMilliseconds: 0)).Status);
    }

    [TestMethod]
    public void CultureInvariantMakesCaseFoldingStableAcrossCurrentCulture()
    {
        System.Globalization.CultureInfo original = System.Globalization.CultureInfo.CurrentCulture;
        try
        {
            System.Globalization.CultureInfo.CurrentCulture = new("tr-TR");
            Assert.AreEqual(RegexRunStatus.NoMatch, RegexToolsService.Run(new("i", "I", Options: RegexOptions.IgnoreCase)).Status);
            Assert.AreEqual(RegexRunStatus.Matches, RegexToolsService.Run(new("i", "I", Options: RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)).Status);
        }
        finally { System.Globalization.CultureInfo.CurrentCulture = original; }
    }

    [TestMethod]
    public void NumberedNamedAndUnmatchedGroupsExposeCorrectDetails()
    {
        RegexResult result = RegexToolsService.Run(new("(?<word>\\w+)-(\\d+)?", "abc-42 x-"));
        Assert.AreEqual(2, result.Matches.Count);
        var first = result.Matches[0].Groups.ToDictionary(group => group.Name);
        Assert.AreEqual("abc", first["word"].Value); Assert.AreEqual(0, first["word"].Index);
        Assert.AreEqual("42", first["1"].Value); Assert.IsTrue(first["1"].Success);
        var second = result.Matches[1].Groups.ToDictionary(group => group.Name);
        Assert.IsFalse(second["1"].Success); Assert.AreEqual(0, second["1"].Length);
    }

    [TestMethod]
    public void ReplacementSupportsNumberedNamedAndPreservesNoMatchInput()
    {
        Assert.AreEqual("42:abc", RegexToolsService.Run(new("(?<word>\\w+)-(\\d+)", "abc-42", "$1:${word}")).Replacement);
        Assert.AreEqual("untouched", RegexToolsService.Run(new("z+", "untouched", "x")).Replacement);
        Assert.AreEqual("xx", RegexToolsService.Run(new("a", "aa", "x")).Replacement);
    }

    [DataTestMethod][DataRow("(")][DataRow("\\k")][DataRow("[")]
    public void InvalidPatternIsReportedWithoutExposingInput(string pattern)
    {
        RegexResult result = RegexToolsService.Run(new(pattern, "private test text", "private replacement"));
        Assert.AreEqual(RegexRunStatus.Invalid, result.Status);
        Assert.IsFalse(result.Message.Contains(pattern));
        Assert.IsFalse(result.Message.Contains("private"));
    }

    [TestMethod]
    public void CatastrophicMatchTimesOutWithoutThrowing()
    {
        RegexResult result = RegexToolsService.Run(new("(a+)+$", new string('a', 80) + "!", TimeoutMilliseconds: 100));
        Assert.AreEqual(RegexRunStatus.TimedOut, result.Status);
        Assert.IsTrue(result.Message.Contains("灾难性回溯"));
        Assert.AreEqual(RegexRunStatus.TimedOut, RegexToolsService.Run(new("(a+)+$", new string('a', 80) + "!", "x", TimeoutMilliseconds: 100)).Status);
    }

    [TestMethod]
    public void ZeroLengthAnchorsEmptyInputsAndUnicode()
    {
        RegexResult boundary = RegexToolsService.Run(new("\\b", "中文 emoji 😀"));
        Assert.IsTrue(boundary.Matches.Count > 0);
        Assert.IsTrue(boundary.Matches.All(match => match.Length == 0));
        Assert.AreEqual(3, RegexToolsService.Run(new("", "ab")).Matches.Count);
        Assert.AreEqual(1, RegexToolsService.Run(new("^$", "")).Matches.Count);
        Assert.AreEqual(RegexRunStatus.Matches, RegexToolsService.Run(new("😀", "你好 😀 世界")).Status);
    }

    [TestMethod]
    public void MatchDisplayLimitStopsAtTenThousandAndFlagsLimited()
    {
        RegexResult result = RegexToolsService.Run(new("(?=a)", new string('a', RegexToolsService.MaximumMatches + 1), ""));
        Assert.AreEqual(RegexRunStatus.Limited, result.Status);
        Assert.AreEqual(RegexToolsService.MaximumMatches, result.Matches.Count);
        Assert.IsTrue(result.Message.Contains("10,000"));
    }

    [TestMethod]
    public void InputLimitsAndReplacementExpansionAreBounded()
    {
        Assert.IsTrue(RegexToolsService.AllowsLive(new string('x', RegexToolsService.LiveInputBytes - 1)));
        Assert.IsFalse(RegexToolsService.AllowsLive(new string('x', RegexToolsService.LiveInputBytes)));
        Assert.AreEqual(RegexRunStatus.TooLarge, RegexToolsService.Run(new(".", new string('x', RegexToolsService.MaximumInputBytes + 1))).Status);
        RegexResult limited = RegexToolsService.Run(new(".", new string('x', 5000), "$`"));
        Assert.AreEqual(RegexRunStatus.Limited, limited.Status);
        Assert.AreEqual(RegexRunStatus.TooLarge, RegexToolsService.Run(new("x", "x",
            new string('r', RegexToolsService.MaximumPatternCharacters + 1))).Status);
    }

    [TestMethod]
    public async Task ViewModelDebouncesCancelsAndRejectsStaleWork()
    {
        using ManualResetEventSlim started = new(); using ManualResetEventSlim release = new();
        int calls = 0;
        RegexToolsViewModel vm = new(new MemoryClipboard(), (request, token) =>
        {
            Interlocked.Increment(ref calls);
            if (request.Pattern == "old") { started.Set(); release.Wait(token); }
            return RegexToolsService.Run(request, token);
        });
        vm.Activate(); vm.Pattern = "old"; vm.Text = "old";
        Task first = vm.PendingTask;
        Assert.IsTrue(started.Wait(TimeSpan.FromSeconds(3)));
        vm.Pattern = "new"; vm.Text = "new";
        Task latest = vm.PendingTask;
        release.Set(); await Task.WhenAll(first, latest);
        Assert.AreEqual("new", vm.Matches.Single().Value);
        Assert.IsTrue(calls >= 2);
        vm.Text = new string('x', RegexToolsService.LiveInputBytes);
        Assert.IsFalse(vm.LiveAllowed); Assert.IsTrue(vm.RequiresManualRun);
        vm.Text = new string('x', RegexToolsService.MaximumInputBytes + 1);
        await vm.RunAsync(); Assert.AreEqual(RegexRunStatus.TooLarge, vm.StatusCode);
        vm.Dispose(); Assert.AreEqual(0, vm.Matches.Count);
    }

    [TestMethod]
    public async Task CopyUsesInjectedClipboardAndDisposeClearsSensitiveInputs()
    {
        MemoryClipboard clipboard = new(); using RegexToolsViewModel vm = new(clipboard);
        vm.Pattern = "(?<x>秘密)"; vm.Text = "秘密"; vm.Replacement = "已替换"; await vm.RunAsync();
        vm.CopyCommand.Execute(vm.ReplacementPreview); Assert.AreEqual("已替换", clipboard.Value);
        vm.Dispose(); Assert.AreEqual("", vm.Pattern); Assert.AreEqual("", vm.Text); Assert.AreEqual("", vm.Replacement);
        Assert.AreEqual(0, vm.Matches.Count); Assert.AreEqual("", vm.ReplacementPreview);
    }

    [TestMethod]
    public async Task ExplicitCancelDoesNotApplyLateWorkerResult()
    {
        using ManualResetEventSlim started = new();
        RegexToolsViewModel vm = new(new MemoryClipboard(), (request, token) =>
        {
            started.Set(); token.WaitHandle.WaitOne(); token.ThrowIfCancellationRequested();
            return RegexToolsService.Run(request);
        });
        vm.Activate(); vm.Pattern = "slow"; vm.Text = "input"; Task pending = vm.PendingTask;
        Assert.IsTrue(started.Wait(TimeSpan.FromSeconds(3)));
        vm.CancelCommand.Execute(null); await pending;
        Assert.AreEqual(RegexRunStatus.Cancelled, vm.StatusCode);
        Assert.AreEqual(0, vm.Matches.Count);
        vm.Dispose();
    }

    private sealed class MemoryClipboard : IUtilitiesTextClipboardAdapter { public string? Value; public void SetText(string text) => Value = text; }
}
