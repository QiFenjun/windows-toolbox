using System.IO;
using WindowsToolbox.Modules.Utilities.Image.Services;
using WindowsToolbox.Modules.Utilities.Image.ViewModels;
using WindowsToolbox.Modules.Utilities.Image.Views;

namespace WindowsToolbox.Tests;

[TestClass]
public sealed class ImageToolsViewModelTests
{
    [TestMethod]
    public async Task SelectionLoadsPreviewAndUpdatesBothAspectDimensions()
    {
        string root = ImageToolsTests.FixtureRoot();
        try
        {
            string path = await ImageToolsService.OnWorkerAsync(() => ImageToolsTests.CreateImage(root, "png"));
            using ImageToolsViewModel vm = new();
            vm.AddFiles([path, path, root]); await vm.PreviewTask;
            Assert.AreEqual(1, vm.Items.Count);
            Assert.IsNotNull(vm.Preview);
            vm.WidthText = "200"; Assert.AreEqual("100", vm.HeightText);
            vm.HeightText = "50"; Assert.AreEqual("100", vm.WidthText);
            vm.FormatIndex = 1; Assert.IsTrue(vm.IsJpeg);
            Assert.IsTrue(vm.OutputPreview.Contains("_resized.jpg"));
            vm.KeepAspect = false; vm.WidthText = "150"; Assert.AreEqual("50", vm.HeightText);
            vm.Clear(); Assert.AreEqual(0, vm.Items.Count); Assert.IsNull(vm.Preview);
        }
        finally { Directory.Delete(root, true); }
    }

    [TestMethod]
    public async Task BatchUiFinishesAndValidationDoesNotWrite()
    {
        string root = ImageToolsTests.FixtureRoot();
        try
        {
            string path = await ImageToolsService.OnWorkerAsync(() => ImageToolsTests.CreateImage(root, "png"));
            using ImageToolsViewModel vm = new(); vm.AddFiles([path]); await vm.PreviewTask;
            vm.WidthText = "0"; await vm.RunAsync();
            Assert.AreEqual(1, Directory.GetFiles(root).Length); Assert.IsFalse(vm.IsBusy);
            vm.WidthText = "20"; await vm.RunAsync();
            Assert.IsFalse(vm.IsBusy); Assert.IsTrue(vm.ProgressText.Contains("Completed 1"));
            vm.Dispose(); Assert.AreEqual(0, vm.Items.Count); Assert.IsNull(vm.Preview);
        }
        finally { Directory.Delete(root, true); }
    }

    [DataTestMethod][DataRow("Light")][DataRow("Dark")]
    public async Task ThemeLoads(string theme) => await ImageToolsService.OnWorkerAsync(() =>
    {
        using ImageToolsViewModel vm = new();
        ToolViewThemeTests.Verify(new ImageToolsView { DataContext = vm }, theme);
        return true;
    });
}
