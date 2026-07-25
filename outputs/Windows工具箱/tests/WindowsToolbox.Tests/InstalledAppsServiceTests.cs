using WindowsToolbox.Modules.InstalledApps.Models;
using WindowsToolbox.Modules.InstalledApps.Services;
using WindowsToolbox.Modules.InstalledApps.ViewModels;

namespace WindowsToolbox.Tests;

[TestClass]
public sealed class InstalledAppsServiceTests
{
    [TestMethod]
    public async Task FakeProviders_AreCombinedAndDeduplicated()
    {
        InstalledApplication first = Create("One", "1.0", "Vendor", "one.exe");
        InstalledApplication duplicate = Create("One", "1.0", "Vendor", "one.exe");
        InstalledApplication second = Create("Two", "2.0", "Vendor", "two.exe");
        InstalledAppService service = new(
            [new FakeProvider([first, second]), new FakeProvider([duplicate])]);

        IReadOnlyList<InstalledApplication> result =
            await service.LoadAsync(CancellationToken.None);

        Assert.AreEqual(2, result.Count);
    }

    [TestMethod]
    public void SearchAndSortingInputs_ProduceExpectedResults()
    {
        InstalledApplication alpha = WithSize(
            Create("Alpha Editor", "1.0", "A Corp", "alpha.exe"),
            100);
        InstalledApplication beta = WithSize(
            Create("Beta Tool", "2.0", "B Corp", "beta.exe"),
            500);
        InstalledApplication[] values = [alpha, beta];

        IReadOnlyList<InstalledApplication> searched = InstalledAppQuery.Apply(
            values,
            "editor",
            "名称",
            "全部发布者",
            "全部来源",
            false);
        IReadOnlyList<InstalledApplication> sorted = InstalledAppQuery.Apply(
            values,
            string.Empty,
            "大小（从大到小）",
            "全部发布者",
            "全部来源",
            false);

        Assert.AreEqual(1, searched.Count);
        Assert.AreEqual("Alpha Editor", searched[0].DisplayName);
        Assert.AreEqual("Beta Tool", sorted[0].DisplayName);
    }

    [TestMethod]
    public async Task UninstallRequiresConfirmationBeforeFakeServiceIsCalled()
    {
        InstalledApplication application = Create("Safe App", "1.0", "Vendor", @"C:\Safe\uninstall.exe");
        FakeActionService actions = new();
        InstalledAppsViewModel viewModel = new(
            new InstalledAppService([new FakeProvider([application])]),
            new ApplicationSizeService(
                new ApplicationSizeCache(Path.Combine(Path.GetTempPath(), Guid.NewGuid() + ".json"))),
            actions,
            new FakeClipboardService());

        await WaitUntilAsync(() => !viewModel.IsLoading && viewModel.VisibleApplications.Count == 1);
        Assert.AreEqual(0, actions.UninstallCallCount);

        viewModel.RequestUninstallCommand.Execute(null);

        Assert.IsTrue(viewModel.IsUninstallConfirmationVisible);
        Assert.AreEqual(0, actions.UninstallCallCount);

        viewModel.ConfirmUninstallCommand.Execute(null);
        await WaitUntilAsync(() => actions.UninstallCallCount == 1);

        Assert.AreEqual(1, actions.UninstallCallCount);
    }

    private static InstalledApplication Create(
        string name,
        string version,
        string publisher,
        string uninstall) =>
        new()
        {
            Id = $"{name}-{version}",
            DisplayName = name,
            DisplayVersion = version,
            Publisher = publisher,
            UninstallString = uninstall,
            CanUninstall = true
        };

    private static InstalledApplication WithSize(
        InstalledApplication source,
        long size) =>
        new()
        {
            Id = source.Id,
            DisplayName = source.DisplayName,
            DisplayVersion = source.DisplayVersion,
            Publisher = source.Publisher,
            UninstallString = source.UninstallString,
            CanUninstall = source.CanUninstall,
            ReportedSizeBytes = size
        };

    private sealed class FakeProvider(
        IReadOnlyList<InstalledApplication> applications) : IInstalledAppProvider
    {
        public Task<IReadOnlyList<InstalledApplication>> GetInstalledApplicationsAsync(
            CancellationToken cancellationToken) =>
            Task.FromResult(applications);
    }

    private sealed class FakeActionService : IApplicationActionService
    {
        public int UninstallCallCount { get; private set; }

        public bool OpenInstallLocation(
            InstalledApplication application,
            out string errorMessage)
        {
            errorMessage = string.Empty;
            return true;
        }

        public Task<UninstallResult> UninstallAsync(
            InstalledApplication application,
            CancellationToken cancellationToken)
        {
            UninstallCallCount++;
            return Task.FromResult(UninstallResult.Failed("测试不会启动真实卸载程序。"));
        }
    }

    private sealed class FakeClipboardService : IApplicationClipboardService
    {
        public void CopyText(string text)
        {
        }
    }

    private static async Task WaitUntilAsync(
        Func<bool> condition,
        int timeoutMilliseconds = 3000)
    {
        DateTime deadline = DateTime.UtcNow.AddMilliseconds(timeoutMilliseconds);
        while (!condition())
        {
            if (DateTime.UtcNow >= deadline)
                Assert.Fail("等待异步状态超时。");
            await Task.Delay(25);
        }
    }
}
