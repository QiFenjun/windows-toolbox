using System.Text.Json;
using WindowsToolbox.Core.Services;

namespace WindowsToolbox.Tests;

[TestClass]
public sealed class SettingsServiceConcurrencyTests
{
    [TestMethod]
    public async Task SaveAsync_ConcurrentSnapshotsLeaveValidAtomicSettingsFile()
    {
        string directory = Path.Combine(Path.GetTempPath(), "WindowsToolbox.Settings.Tests", Guid.NewGuid().ToString("N"));
        string settingsPath = Path.Combine(directory, "settings.json");
        SettingsService settings = new(settingsPath);
        try
        {
            Task[] saves = Enumerable.Range(0, 100).Select(index =>
            {
                settings.Settings.RecentModuleIds = [$"module-{index}", new string('x', 4096)];
                return settings.SaveAsync();
            }).ToArray();
            await Task.WhenAll(saves);

            using JsonDocument document = JsonDocument.Parse(await File.ReadAllBytesAsync(settingsPath));
            JsonElement recent = document.RootElement.GetProperty("RecentModuleIds");
            Assert.AreEqual(2, recent.GetArrayLength());
            StringAssert.StartsWith(recent[0].GetString()!, "module-");
            Assert.AreEqual(4096, recent[1].GetString()!.Length);
            Assert.IsFalse(File.Exists(settingsPath + ".tmp"));
        }
        finally
        {
            if (Directory.Exists(directory)) Directory.Delete(directory, recursive: true);
        }
    }
}
