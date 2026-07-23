using WindowsToolbox.Modules.Shutdown.Models;
using WindowsToolbox.Modules.Shutdown.Services;

namespace WindowsToolbox.Tests;

[TestClass]
public sealed class ShutdownServiceTests
{
    [TestMethod]
    public void ValidateShutdownTime_RejectsPastTime()
    {
        ShutdownService service = new();
        ShutdownOperationResult result = service.ValidateShutdownTime(DateTime.Now.AddMinutes(-1));

        Assert.IsFalse(result.IsSuccess);
        Assert.AreEqual(ShutdownError.InvalidTime, result.Error);
    }

    [TestMethod]
    public void ValidateShutdownTime_RejectsLessThanOneMinute()
    {
        ShutdownService service = new();
        ShutdownOperationResult result = service.ValidateShutdownTime(DateTime.Now.AddSeconds(30));

        Assert.IsFalse(result.IsSuccess);
        Assert.AreEqual(ShutdownError.InvalidTime, result.Error);
    }

    [TestMethod]
    public void ValidateShutdownTime_AcceptsFutureTime()
    {
        ShutdownService service = new();
        ShutdownOperationResult result = service.ValidateShutdownTime(DateTime.Now.AddMinutes(30));

        Assert.IsTrue(result.IsSuccess);
        Assert.AreEqual(ShutdownError.None, result.Error);
    }
}
