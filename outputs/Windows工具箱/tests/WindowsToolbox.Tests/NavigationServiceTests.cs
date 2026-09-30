using WindowsToolbox.Core.Interfaces;
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

    [TestMethod]
    public void Navigate_ReentrantRequestKeepsNewestPageAndReportsStaleFactory()
    {
        NavigationService navigation = new();
        object newest = new();
        List<NavigationDiagnosticEventArgs> diagnostics = [];
        navigation.Diagnostic += (_, eventArgs) => diagnostics.Add(eventArgs);
        navigation.Register("newest", () => newest);
        navigation.Register("older", () =>
        {
            Assert.IsTrue(navigation.Navigate("newest"));
            return new object();
        });

        Assert.IsFalse(navigation.Navigate("older"));

        Assert.AreEqual("newest", navigation.CurrentPageId);
        Assert.AreSame(newest, navigation.CurrentViewModel);
        Assert.IsTrue(diagnostics.Any(item =>
            item.RequestId == 1 && item.TargetPageId == "older" && item.Stage == NavigationDiagnosticStage.Stale));
        Assert.AreEqual(2, diagnostics.Where(item => item.Stage == NavigationDiagnosticStage.Requested).Count());
    }

    [TestMethod]
    public void Navigate_FactoryFailureKeepsCurrentPageAndAllowsRecovery()
    {
        NavigationService navigation = new();
        object home = new();
        object recovered = new();
        navigation.Register("home", () => home);
        navigation.Register("broken", () => throw new InvalidOperationException("controlled factory failure"));
        navigation.Register("recovered", () => recovered);

        Assert.IsTrue(navigation.Navigate("home"));
        Assert.ThrowsException<InvalidOperationException>(() => navigation.Navigate("broken"));
        Assert.AreEqual("home", navigation.CurrentPageId);
        Assert.AreSame(home, navigation.CurrentViewModel);
        Assert.IsTrue(navigation.Navigate("recovered"));
        Assert.AreSame(recovered, navigation.CurrentViewModel);
    }
}
