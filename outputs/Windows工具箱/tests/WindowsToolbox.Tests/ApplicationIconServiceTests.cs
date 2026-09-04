using System.Windows.Media;
using System.Windows;
using WindowsToolbox.Modules.InstalledApps.Models;
using WindowsToolbox.Modules.InstalledApps.Services;

namespace WindowsToolbox.Tests;

[TestClass]
public sealed class ApplicationIconServiceTests
{
    [DataTestMethod]
    [DataRow(@"C:\Program Files\Foo\Foo.exe", null, @"C:\Program Files\Foo\Foo.exe")]
    [DataRow("\"C:\\Program Files\\Foo\\Foo.exe\"", null, @"C:\Program Files\Foo\Foo.exe")]
    [DataRow("\"C:\\Program Files\\Foo\\Foo.exe\",0", 0, @"C:\Program Files\Foo\Foo.exe")]
    [DataRow(@"C:\Program Files\Foo\Foo.exe,1", 1, @"C:\Program Files\Foo\Foo.exe")]
    [DataRow("\"C:\\Program Files\\Foo\\Foo.dll\",-123", -123, @"C:\Program Files\Foo\Foo.dll")]
    [DataRow("\"C:\\Path,With,Comma\\Foo.exe\",0", 0, @"C:\Path,With,Comma\Foo.exe")]
    public void DisplayIconParser_ParsesPathsAndResourceIndices(
        string input,
        int? expectedIndex,
        string expectedPath)
    {
        ParsedDisplayIcon? parsed = DisplayIconParser.Parse(input);

        Assert.IsNotNull(parsed);
        Assert.AreEqual(expectedPath, parsed.Path);
        Assert.AreEqual(expectedIndex, parsed.IconIndex);
    }

    [TestMethod]
    public void DisplayIconParser_ExpandsEnvironmentVariables()
    {
        ParsedDisplayIcon? parsed = DisplayIconParser.Parse("%SystemRoot%\\System32\\shell32.dll,-1");

        Assert.IsNotNull(parsed);
        StringAssert.StartsWith(parsed.Path, Environment.GetEnvironmentVariable("SystemRoot")!);
        Assert.AreEqual(-1, parsed.IconIndex);
    }

    [TestMethod]
    public void CandidateResolver_DoesNotTreatUninstallerOrMsiExecAsApplication()
    {
        using TemporaryIconFiles files = new("uninstall.exe", "msiexec.exe");
        InstalledApplication app = new()
        {
            DisplayName = "Example App",
            InstallLocation = files.DirectoryPath,
            UninstallString = Path.Combine(files.DirectoryPath, "uninstall.exe")
        };

        IReadOnlyList<ApplicationIconCandidate> candidates = ApplicationIconCandidateResolver.Resolve(app);

        Assert.AreEqual(0, candidates.Count);
    }

    [TestMethod]
    public void CandidateResolver_RetriesPrimaryExecutableWhenDisplayIconIndexFails()
    {
        using TemporaryIconFiles files = new("Demo.exe");
        string executable = files.FilePaths[0];
        InstalledApplication app = new()
        {
            DisplayName = "Demo",
            DisplayIconPath = $"\"{executable}\",999",
            InstallLocation = executable
        };

        IReadOnlyList<ApplicationIconCandidate> candidates = ApplicationIconCandidateResolver.Resolve(app);

        CollectionAssert.AreEqual(new int?[] { 999, 0 }, candidates.Select(candidate => candidate.IconIndex).ToArray());
    }

    [TestMethod]
    public async Task IconService_CachesSameSourceAndInvalidatesWhenFileChanges()
    {
        using TemporaryIconFiles files = new("Demo.exe");
        string executable = files.FilePaths[0];
        CountingExtractor extractor = new();
        ApplicationIconService service = new(extractor);
        InstalledApplication app = CreateApplication(executable);

        ApplicationIconResult first = await service.GetIconResultAsync(app, 48, CancellationToken.None);
        ApplicationIconResult second = await service.GetIconResultAsync(app, 48, CancellationToken.None);
        File.SetLastWriteTimeUtc(executable, DateTime.UtcNow.AddMinutes(1));
        ApplicationIconResult changed = await service.GetIconResultAsync(app, 48, CancellationToken.None);

        Assert.IsNotNull(first.Icon);
        Assert.IsNotNull(second.Icon);
        Assert.IsNotNull(changed.Icon);
        Assert.AreEqual(2, extractor.CallCount);
        Assert.AreEqual(ApplicationIconSource.DisplayIcon, first.Source);
    }

    [TestMethod]
    public async Task IconService_ReturnsFallbackWhenFileIsMissingOrExtractorFails()
    {
        ApplicationIconService missingService = new(new CountingExtractor());
        ApplicationIconResult missing = await missingService.GetIconResultAsync(
            CreateApplication(Path.Combine(Path.GetTempPath(), "missing-app.exe")),
            48,
            CancellationToken.None);

        using TemporaryIconFiles files = new("Broken.exe");
        ApplicationIconService failingService = new(new CountingExtractor(returnNull: true));
        ApplicationIconResult failed = await failingService.GetIconResultAsync(
            CreateApplication(files.FilePaths[0]),
            48,
            CancellationToken.None);

        Assert.IsNull(missing.Icon);
        Assert.AreEqual(ApplicationIconSource.Default, missing.Source);
        Assert.IsNull(failed.Icon);
        Assert.AreEqual(ApplicationIconSource.Default, failed.Source);
    }

    [TestMethod]
    public async Task IconService_LimitsConcurrentShellExtractions()
    {
        using TemporaryIconFiles files = new(Enumerable.Range(0, 10).Select(index => $"App{index}.exe").ToArray());
        CountingExtractor extractor = new(delayMilliseconds: 30);
        ApplicationIconService service = new(extractor, maxConcurrentExtractions: 2);

        await Task.WhenAll(files.FilePaths.Select(path => service.GetIconResultAsync(
            CreateApplication(path), 48, CancellationToken.None)));

        Assert.IsTrue(extractor.MaximumConcurrentCalls <= 2);
        Assert.AreEqual(10, extractor.CallCount);
    }

    [TestMethod]
    public async Task IconService_OneFailedApplicationDoesNotAffectOtherApplications()
    {
        using TemporaryIconFiles files = new("bad.exe", "good.exe");
        ApplicationIconService service = new(new CountingExtractor(failWhenPathContains: "bad"));

        ApplicationIconResult[] results = await Task.WhenAll(files.FilePaths.Select(path => service.GetIconResultAsync(
            CreateApplication(path), 48, CancellationToken.None)));

        Assert.AreEqual(ApplicationIconSource.Default, results.Single(result => result.SourcePath is null).Source);
        Assert.AreEqual(1, results.Count(result => result.Icon is not null));
    }

    [TestMethod]
    public async Task IconService_ReturnsFrozenImageForCrossThreadBinding()
    {
        using TemporaryIconFiles files = new("Frozen.exe");
        ApplicationIconResult result = await new ApplicationIconService(new CountingExtractor())
            .GetIconResultAsync(CreateApplication(files.FilePaths[0]), 48, CancellationToken.None);

        Assert.IsInstanceOfType<Freezable>(result.Icon);
        Assert.IsTrue(((Freezable)result.Icon!).IsFrozen);
    }

    [TestMethod]
    public void ShellExtractor_ExtractsAndFreezesSystemDllResource()
    {
        string shell32 = Path.Combine(Environment.SystemDirectory, "shell32.dll");
        ImageSource? icon = new ShellApplicationIconExtractor().Extract(shell32, -1, 48);

        Assert.IsNotNull(icon);
        Assert.IsInstanceOfType<Freezable>(icon);
        Assert.IsTrue(((Freezable)icon).IsFrozen);
    }

    private static InstalledApplication CreateApplication(string displayIconPath) => new()
    {
        Id = Guid.NewGuid().ToString("N"),
        DisplayName = "Demo",
        DisplayIconPath = displayIconPath
    };

    private sealed class CountingExtractor : IApplicationIconExtractor
    {
        private readonly int _delayMilliseconds;
        private readonly bool _returnNull;
        private readonly string? _failWhenPathContains;
        private int _callCount;
        private int _currentConcurrentCalls;
        private int _maximumConcurrentCalls;

        public CountingExtractor(int delayMilliseconds = 0, bool returnNull = false, string? failWhenPathContains = null)
        {
            _delayMilliseconds = delayMilliseconds;
            _returnNull = returnNull;
            _failWhenPathContains = failWhenPathContains;
        }

        public int CallCount => _callCount;
        public int MaximumConcurrentCalls => _maximumConcurrentCalls;

        public ImageSource? Extract(string sourcePath, int? iconIndex, int desiredSize)
        {
            int concurrent = Interlocked.Increment(ref _currentConcurrentCalls);
            Interlocked.Increment(ref _callCount);
            UpdateMaximum(concurrent);
            try
            {
                if (_delayMilliseconds > 0)
                    Thread.Sleep(_delayMilliseconds);
                if (_returnNull || (_failWhenPathContains is not null && sourcePath.Contains(_failWhenPathContains, StringComparison.OrdinalIgnoreCase)))
                    return null;

                DrawingImage image = new();
                image.Freeze();
                return image;
            }
            finally
            {
                Interlocked.Decrement(ref _currentConcurrentCalls);
            }
        }

        private void UpdateMaximum(int value)
        {
            int current;
            do
            {
                current = _maximumConcurrentCalls;
                if (value <= current)
                    return;
            }
            while (Interlocked.CompareExchange(ref _maximumConcurrentCalls, value, current) != current);
        }
    }

    private sealed class TemporaryIconFiles : IDisposable
    {
        public TemporaryIconFiles(params string[] fileNames)
        {
            DirectoryPath = Path.Combine(Path.GetTempPath(), "WindowsToolboxIconTests", Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(DirectoryPath);
            FilePaths = fileNames.Select(name => Path.Combine(DirectoryPath, name)).ToArray();
            foreach (string path in FilePaths)
                File.WriteAllBytes(path, []);
        }

        public string DirectoryPath { get; }
        public string[] FilePaths { get; }

        public void Dispose()
        {
            if (Directory.Exists(DirectoryPath))
                Directory.Delete(DirectoryPath, recursive: true);
        }
    }
}
