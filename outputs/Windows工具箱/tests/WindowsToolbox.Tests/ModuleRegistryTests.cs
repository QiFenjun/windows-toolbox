using WindowsToolbox.Core.Interfaces;
using WindowsToolbox.Core.Services;

namespace WindowsToolbox.Tests;

[TestClass]
public sealed class ModuleRegistryTests
{
    [TestMethod]
    public void Register_SortsAndFindsModules()
    {
        ModuleRegistry registry = new();
        registry.Register(new FakeModule("second", 20));
        registry.Register(new FakeModule("first", 10));

        Assert.AreEqual("first", registry.Modules[0].Id);
        Assert.AreEqual("second", registry.Find("SECOND")?.Id);
    }

    [TestMethod]
    public void Search_MatchesDescriptionAndKeywords()
    {
        ModuleRegistry registry = new();
        registry.Register(new FakeModule("shutdown", 10, "定时关机", "创建关机计划", ["电源", "倒计时"]));

        Assert.AreEqual(1, registry.Search("倒计时").Count);
        Assert.AreEqual(1, registry.Search("创建").Count);
        Assert.AreEqual(0, registry.Search("截图").Count);
    }

    [TestMethod]
    public void Register_RejectsDuplicateId()
    {
        ModuleRegistry registry = new();
        registry.Register(new FakeModule("tool", 10));

        Assert.ThrowsException<InvalidOperationException>(() =>
            registry.Register(new FakeModule("TOOL", 20)));
    }

    private sealed class FakeModule(
        string id,
        int sortOrder,
        string? name = null,
        string? description = null,
        IReadOnlyList<string>? keywords = null) : IToolModule
    {
        public string Id => id;
        public string DisplayName => name ?? id;
        public string Description => description ?? id;
        public string Category => "测试";
        public string IconKey => "Toolbox";
        public int SortOrder => sortOrder;
        public bool IsAvailable => true;
        public IReadOnlyList<string> Keywords => keywords ?? [];
        public string? ResourceDictionaryPath => null;
        public object CreateViewModel() => new();
    }
}
