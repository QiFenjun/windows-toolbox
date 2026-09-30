using WindowsToolbox.Core.Services;

namespace WindowsToolbox.Tests;

[TestClass]
public sealed class SettingsServiceLanguageTests
{
    [TestMethod]
    public async Task Language_MigratesOldSettingsPersistsAndFallsBackForUnknownValues()
    {
        string directory = Path.Combine(Path.GetTempPath(), "WindowsToolbox.Language.Tests", Guid.NewGuid().ToString("N"));
        string path = Path.Combine(directory, "settings.json");
        Directory.CreateDirectory(directory);
        try
        {
            await File.WriteAllTextAsync(path, "{\"Theme\":\"Dark\"}");
            SettingsService settings = new(path);
            await settings.LoadAsync();
            Assert.AreEqual("zh-CN", settings.Settings.Language);
            await settings.SaveAsync();
            SettingsService chineseReload = new(path);
            await chineseReload.LoadAsync();
            Assert.AreEqual("zh-CN", chineseReload.Settings.Language);

            settings.Settings.Language = "en-US";
            await settings.SaveAsync();
            SettingsService reloaded = new(path);
            await reloaded.LoadAsync();
            Assert.AreEqual("en-US", reloaded.Settings.Language);

            await File.WriteAllTextAsync(path, "{\"Language\":\"fr-FR\"}");
            await reloaded.LoadAsync();
            Assert.AreEqual("zh-CN", reloaded.Settings.Language);
        }
        finally
        {
            if (Directory.Exists(directory)) Directory.Delete(directory, recursive: true);
        }
    }
}
