using System.Security.Cryptography;
using System.Text;
using WindowsToolbox.Core.Interfaces;
using WindowsToolbox.Modules.FileTools;
using WindowsToolbox.Modules.FileTools.Models;
using WindowsToolbox.Modules.FileTools.Services;

namespace WindowsToolbox.Tests;

[TestClass]
public sealed class FileToolsTests
{
    private string _root = string.Empty;

    [TestInitialize]
    public void SetUp()
    {
        _root = Path.Combine(Path.GetTempPath(), "WindowsToolbox.FileTools.Tests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(_root);
    }

    [TestCleanup]
    public void TearDown()
    {
        try { if (Directory.Exists(_root)) Directory.Delete(_root, true); } catch { }
    }

    [TestMethod]
    public void ModuleMetadataIsStable()
    {
        IToolModule module = new FileToolsModule();
        Assert.AreEqual("file-tools", module.Id);
        Assert.AreEqual("文件工具", module.DisplayName);
        Assert.AreEqual("File Tools", module.EnglishName);
        Assert.AreEqual("效率工具", module.Category);
        StringAssert.Contains(module.Description, "文件");
    }

    [TestMethod]
    public void SelectionDeduplicatesWindowsPaths()
    {
        string file = CreateFile("A.txt", "a");
        FileSelectionService selection = new();
        selection.Add([file, file.ToUpperInvariant(), Path.Combine(_root, ".", "A.txt")]);
        Assert.AreEqual(1, selection.Items.Count);
    }

    [TestMethod]
    public void PrefixIsAppliedBeforeExtension()
    {
        string file = CreateFile("photo.jpg", "x");
        RenamePreviewItem item = new RenameService().BuildPreview([file], new RenameRuleOptions { Prefix = "holiday_" }).Single();
        Assert.AreEqual("holiday_photo.jpg", item.NewName);
        Assert.AreEqual(RenameItemStatus.Ready, item.Status);
    }

    [TestMethod]
    public void SuffixIsAppliedBeforeExtension()
    {
        string file = CreateFile("photo.jpg", "x");
        RenamePreviewItem item = new RenameService().BuildPreview([file], new RenameRuleOptions { Suffix = "_final" }).Single();
        Assert.AreEqual("photo_final.jpg", item.NewName);
    }

    [TestMethod]
    public void FindReplaceHonorsCaseOption()
    {
        string file = CreateFile("Photo.JPG", "x");
        RenamePreviewItem insensitive = new RenameService().BuildPreview([file], new RenameRuleOptions { Find = "photo", Replace = "image" }).Single();
        RenamePreviewItem sensitive = new RenameService().BuildPreview([file], new RenameRuleOptions { Find = "photo", Replace = "image", MatchCase = true }).Single();
        Assert.AreEqual("image.JPG", insensitive.NewName);
        Assert.AreEqual("Photo.JPG", sensitive.NewName);
        Assert.AreEqual(RenameItemStatus.Unchanged, sensitive.Status);
    }

    [TestMethod] public void LowercaseDoesNotChangeExtension() => Assert.AreEqual("photo.JPG", new RenameService().BuildPreview([CreateFile("PHOTO.JPG", "x")], new RenameRuleOptions { CaseMode = RenameCaseMode.Lowercase }).Single().NewName);
    [TestMethod] public void UppercaseDoesNotChangeExtension() => Assert.AreEqual("PHOTO.jpg", new RenameService().BuildPreview([CreateFile("photo.jpg", "x")], new RenameRuleOptions { CaseMode = RenameCaseMode.Uppercase }).Single().NewName);
    [TestMethod] public void TitleCaseIsAvailable() => Assert.AreEqual("Hello World.txt", new RenameService().BuildPreview([CreateFile("hello world.txt", "x")], new RenameRuleOptions { CaseMode = RenameCaseMode.TitleCase }).Single().NewName);

    [TestMethod]
    public void NumberingUsesStartStepAndDigits()
    {
        string first = CreateFile("a.txt", "a");
        string second = CreateFile("b.txt", "b");
        IReadOnlyList<RenamePreviewItem> items = new RenameService().BuildPreview([first, second], new RenameRuleOptions
        {
            NumberingEnabled = true, NumberStart = 5, NumberStep = 2, NumberDigits = 3, NumberSuffix = "_"
        });
        CollectionAssert.AreEqual(new[] { "005_a.txt", "007_b.txt" }, items.Select(item => item.NewName).ToArray());
    }

    [TestMethod]
    public void ExtensionChangeIsNameOnly()
    {
        string file = CreateFile("photo.jpeg", "not an image");
        RenamePreviewItem item = new RenameService().BuildPreview([file], new RenameRuleOptions { NewExtension = "jpg" }).Single();
        Assert.AreEqual("photo.jpg", item.NewName);
        StringAssert.Contains(item.Message, string.Empty);
    }

    [TestMethod]
    public void InvalidCharacterIsRejected()
    {
        string file = CreateFile("photo.txt", "x");
        RenamePreviewItem item = new RenameService().BuildPreview([file], new RenameRuleOptions { Suffix = "*" }).Single();
        Assert.AreEqual(RenameItemStatus.Invalid, item.Status);
    }

    [TestMethod]
    public void TrailingDotIsRejected()
    {
        string file = CreateFile("photo.txt", "x");
        RenamePreviewItem item = new RenameService().BuildPreview([file], new RenameRuleOptions { NewExtension = "." }).Single();
        Assert.AreEqual(RenameItemStatus.Invalid, item.Status);
    }

    [TestMethod]
    public void ReservedDeviceNameIsRejected()
    {
        string file = CreateFile("photo.txt", "x");
        RenamePreviewItem item = new RenameService().BuildPreview([file], new RenameRuleOptions { Find = "photo", Replace = "CON" }).Single();
        Assert.AreEqual(RenameItemStatus.Invalid, item.Status);
    }

    [TestMethod]
    public void DuplicateTargetsAreRejected()
    {
        string first = CreateFile("a.txt", "a");
        string second = CreateFile("a (1).txt", "b");
        IReadOnlyList<RenamePreviewItem> items = new RenameService().BuildPreview([first, second], new RenameRuleOptions { Find = " (1)", Replace = string.Empty });
        Assert.IsTrue(items.Any(item => item.Status == RenameItemStatus.Conflict));
    }

    [TestMethod]
    public void ExistingDestinationIsRejected()
    {
        string source = CreateFile("a.txt", "a");
        CreateFile("b.txt", "b");
        RenamePreviewItem item = new RenameService().BuildPreview([source], new RenameRuleOptions { Find = "a", Replace = "b" }).Single();
        Assert.AreEqual(RenameItemStatus.Conflict, item.Status);
    }

    [TestMethod]
    public void MissingSourceIsReported()
    {
        string missing = Path.Combine(_root, "missing.txt");
        RenamePreviewItem item = new RenameService().BuildPreview([missing], new RenameRuleOptions { Prefix = "x" }).Single();
        Assert.AreEqual(RenameItemStatus.Missing, item.Status);
    }

    [TestMethod]
    public async Task ApplySupportsSwapCycle()
    {
        string a = CreateFile("A.txt", "A");
        string b = CreateFile("B.txt", "B");
        RenameService service = new();
        RenameBatchResult result = await service.ApplyAsync([
            new RenamePreviewItem(a, b, RenameItemStatus.Ready),
            new RenamePreviewItem(b, a, RenameItemStatus.Ready)
        ]);
        Assert.AreEqual(2, result.Succeeded);
        Assert.AreEqual("B", await File.ReadAllTextAsync(a));
        Assert.AreEqual("A", await File.ReadAllTextAsync(b));
    }

    [TestMethod]
    public async Task ApplySupportsThreeFileCycle()
    {
        string a = CreateFile("A.txt", "A");
        string b = CreateFile("B.txt", "B");
        string c = CreateFile("C.txt", "C");
        RenameBatchResult result = await new RenameService().ApplyAsync([
            new RenamePreviewItem(a, b, RenameItemStatus.Ready),
            new RenamePreviewItem(b, c, RenameItemStatus.Ready),
            new RenamePreviewItem(c, a, RenameItemStatus.Ready)
        ]);
        Assert.AreEqual(3, result.Succeeded);
        Assert.AreEqual("C", await File.ReadAllTextAsync(a));
        Assert.AreEqual("A", await File.ReadAllTextAsync(b));
        Assert.AreEqual("B", await File.ReadAllTextAsync(c));
    }

    [TestMethod]
    public async Task ApplyAndUndoAreSessionScoped()
    {
        string source = CreateFile("before.txt", "data");
        string target = Path.Combine(_root, "after.txt");
        RenameService service = new();
        Assert.AreEqual(1, (await service.ApplyAsync([new RenamePreviewItem(source, target, RenameItemStatus.Ready)])).Succeeded);
        Assert.IsTrue(File.Exists(target));
        Assert.AreEqual(1, (await service.UndoLastBatchAsync()).Succeeded);
        Assert.IsTrue(File.Exists(source));
        Assert.AreEqual(0, (await service.UndoLastBatchAsync()).Succeeded);
    }

    [TestMethod]
    public async Task UndoDoesNotOverwriteConflict()
    {
        string source = CreateFile("before.txt", "data");
        string target = Path.Combine(_root, "after.txt");
        RenameService service = new();
        await service.ApplyAsync([new RenamePreviewItem(source, target, RenameItemStatus.Ready)]);
        CreateFile("before.txt", "other");
        RenameBatchResult result = await service.UndoLastBatchAsync();
        Assert.AreEqual(0, result.Succeeded);
        Assert.IsTrue(File.Exists(target));
    }

    [TestMethod]
    public async Task Sha256KnownVector()
    {
        string file = CreateFile("hash.txt", "abc");
        HashResult result = (await new HashService().ComputeAsync([file], "SHA-256")).Single();
        Assert.AreEqual(HashItemStatus.Completed, result.Status);
        Assert.AreEqual("BA7816BF8F01CFEA414140DE5DAE2223B00361A396177A9CB410FF61F20015AD", result.Hash);
    }

    [TestMethod]
    public async Task Sha512KnownVector()
    {
        string file = CreateFile("hash.txt", "abc");
        HashResult result = (await new HashService().ComputeAsync([file], "SHA-512")).Single();
        Assert.AreEqual("DDAF35A193617ABACC417349AE20413112E6FA4E89A97EA20A9EEEE64B55D39A2192992A274FC1A836BA3C23A3FEebBD454D4423643CE80E2A9AC94FA54CA49F", result.Hash, true);
    }

    [TestMethod]
    public async Task Md5IsAvailableForCompatibility()
    {
        string file = CreateFile("hash.txt", "abc");
        HashResult result = (await new HashService().ComputeAsync([file], "MD5")).Single();
        Assert.AreEqual("900150983CD24FB0D6963F7D28E17F72", result.Hash);
    }

    [TestMethod]
    public async Task EmptyFileHashWorks()
    {
        string file = CreateFile("empty.bin", string.Empty);
        HashResult result = (await new HashService().ComputeAsync([file], "SHA-256")).Single();
        Assert.AreEqual(HashItemStatus.Completed, result.Status);
        Assert.AreEqual("E3B0C44298FC1C149AFBF4C8996FB92427AE41E4649B934CA495991B7852B855", result.Hash);
    }

    [TestMethod]
    public async Task HashDeduplicatesFiles()
    {
        string file = CreateFile("hash.txt", "abc");
        IReadOnlyList<HashResult> results = await new HashService().ComputeAsync([file, file.ToUpperInvariant()], "SHA-256");
        Assert.AreEqual(1, results.Count);
    }

    [TestMethod]
    public async Task MissingHashIsReported()
    {
        HashResult result = (await new HashService().ComputeAsync([Path.Combine(_root, "missing.txt")], "SHA-256")).Single();
        Assert.AreEqual(HashItemStatus.Missing, result.Status);
    }

    [TestMethod]
    public async Task HashCancellationDoesNotReturnPartialHash()
    {
        string file = CreateFile("large.bin", new string('x', 4 * 1024 * 1024));
        using CancellationTokenSource cancellation = new();
        cancellation.Cancel();
        HashResult result = await new HashService().ComputeFileAsync(file, "SHA-256", cancellationToken: cancellation.Token);
        Assert.AreEqual(HashItemStatus.Cancelled, result.Status);
        Assert.AreEqual(string.Empty, result.Hash);
    }

    [TestMethod]
    public async Task Sha256SumsUsesTwoSpaceFormat()
    {
        string file = CreateFile("hash.txt", "abc");
        HashService service = new();
        HashResult result = (await service.ComputeAsync([file], "SHA-256")).Single();
        string sums = Path.Combine(_root, "SHA256SUMS.txt");
        await service.GenerateSha256SumsAsync([result], sums, false);
        StringAssert.Contains(await File.ReadAllTextAsync(sums), result.Hash + "  hash.txt");
    }

    [TestMethod]
    public void ExpectedHashIgnoresCaseAndWhitespace() => Assert.IsTrue(HashService.Verify("  abcDEF  ", "ABCdef"));

    [TestMethod]
    public void FullPathAndComponentsWorkForMissingPath()
    {
        string input = Path.Combine(_root, "future file.txt");
        PathToolsService service = new();
        Assert.AreEqual("future file.txt", service.Format(input, PathOutputKind.FileName));
        Assert.AreEqual("future file", service.Format(input, PathOutputKind.FileStem));
        Assert.AreEqual(".txt", service.Format(input, PathOutputKind.Extension));
        Assert.AreEqual(_root, service.Format(input, PathOutputKind.ParentDirectory));
    }

    [TestMethod]
    public void PowerShellLiteralEscapesSingleQuote()
    {
        string input = Path.Combine(_root, "user's file.txt");
        Assert.AreEqual($"'{input.Replace("'", "''")}'", new PathToolsService().Format(input, PathOutputKind.PowerShellLiteralPath));
    }

    [TestMethod]
    public void ForwardSlashAndUriAreGeneratedByFramework()
    {
        string input = Path.Combine(_root, "file.txt");
        PathToolsService service = new();
        StringAssert.DoesNotMatch(service.Format(input, PathOutputKind.ForwardSlash), new System.Text.RegularExpressions.Regex(@"\\"));
        StringAssert.StartsWith(service.Format(input, PathOutputKind.FileUri), "file:");
    }

    [TestMethod]
    public void PathConvertDeduplicatesAndReturnsAllKinds()
    {
        string input = Path.Combine(_root, "file.txt");
        IReadOnlyList<PathResult> results = new PathToolsService().Convert([input, input], [PathOutputKind.FileName, PathOutputKind.Extension]);
        Assert.AreEqual(2, results.Count);
    }

    [TestMethod]
    public void FileInfoReadsFileMetadata()
    {
        string file = CreateFile("meta.txt", "hello");
        FileInfoSnapshot snapshot = new FileInfoService().Read(file);
        Assert.IsTrue(snapshot.Exists);
        Assert.IsFalse(snapshot.IsDirectory);
        Assert.AreEqual(5, snapshot.Size);
        Assert.AreEqual(".txt", snapshot.Extension);
    }

    [TestMethod]
    public void FileInfoReportsMissing()
    {
        FileInfoSnapshot snapshot = new FileInfoService().Read(Path.Combine(_root, "missing.txt"));
        Assert.IsFalse(snapshot.Exists);
        Assert.AreEqual("不存在", snapshot.StatusText);
    }

    [TestMethod]
    public async Task FolderSizeCountsFilesAndDirectories()
    {
        CreateFile("folder/a.txt", "123");
        CreateFile("folder/nested/b.txt", "12");
        FolderSizeResult result = await new FileInfoService().CalculateFolderSizeAsync(Path.Combine(_root, "folder"));
        Assert.AreEqual(2, result.FilesScanned);
        Assert.AreEqual(2, result.DirectoriesScanned);
        Assert.AreEqual(5, result.TotalBytes);
        Assert.AreEqual(0, result.Skipped);
    }

    [TestMethod]
    public async Task FolderSizeCanCancel()
    {
        CreateFile("folder/a.txt", "123");
        using CancellationTokenSource cancellation = new();
        cancellation.Cancel();
        await Assert.ThrowsExceptionAsync<OperationCanceledException>(() => new FileInfoService().CalculateFolderSizeAsync(Path.Combine(_root, "folder"), cancellationToken: cancellation.Token));
    }

    [TestMethod]
    public void FileDropParserRejectsNonFileData()
    {
        Assert.AreEqual(0, FileDropParser.Parse(null).Count);
    }

    private string CreateFile(string relativePath, string content)
    {
        string path = Path.Combine(_root, relativePath);
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        File.WriteAllText(path, content, new UTF8Encoding(false));
        return path;
    }
}
