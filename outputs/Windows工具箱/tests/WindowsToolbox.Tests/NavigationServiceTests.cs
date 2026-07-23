using WindowsToolbox.Core.Services;

namespace WindowsToolbox.Tests;

[TestClass]
public sealed class NavigationServiceTests
{
    [TestMethod]
    public void Navigate_CachesViewModelInstance()
    {
        NavigationService navigation = new();
        int createCount = 0;
        navigation.Register("home", () =>
        {
            createCount++;
            return new object();
        });

        Assert.IsTrue(navigation.Navigate("home"));
        object? first = navigation.CurrentViewModel;
        Assert.IsTrue(navigation.Navigate("home"));

        Assert.AreSame(first, navigation.CurrentViewModel);
        Assert.AreEqual(1, createCount);
    }

    [TestMethod]
    public void Navigate_ReturnsFalseForUnknownPage()
    {
        NavigationService navigation = new();
        Assert.IsFalse(navigation.Navigate("missing"));
    }
}
