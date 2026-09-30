using WindowsToolbox.App.Services;
using WindowsToolbox.App.ViewModels;
using WindowsToolbox.Modules.TextTools;

namespace WindowsToolbox.Tests;

[TestClass]
public sealed class LocalizationTests
{
    [TestMethod]
    public void CachedModuleItemRefreshesNameAndDescriptionWhenLanguageChanges()
    {
        LocalizationService localization = new();
        ModuleItemViewModel module = new(new TextToolsModule(), localization);
        int notifications = 0;
        module.PropertyChanged += (_, _) => notifications++;

        Assert.AreEqual("文本工具", module.DisplayName);
        localization.Apply(LocalizationService.English);
        module.RefreshLocalization();

        Assert.AreEqual("Text Tools", module.DisplayName);
        Assert.AreEqual("Transform, compare, encode, and inspect text.", module.Description);
        Assert.IsTrue(notifications >= 2);
    }
}
