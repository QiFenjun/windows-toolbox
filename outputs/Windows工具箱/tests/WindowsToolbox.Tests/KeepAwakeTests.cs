using System.Collections.Concurrent;
using System.Text.Json;
using WindowsToolbox.Core.Interfaces;
using WindowsToolbox.Core.Models;
using WindowsToolbox.Core.Services;
using WindowsToolbox.Modules.KeepAwake;
using WindowsToolbox.Modules.KeepAwake.Models;
using WindowsToolbox.Modules.KeepAwake.Services;
using WindowsToolbox.Modules.KeepAwake.ViewModels;

namespace WindowsToolbox.Tests;

[TestClass]
public sealed class KeepAwakeTests
{
    [TestMethod] public void MetadataAndStaticRegistration()
    {
        using KeepAwakeModule module=new(new AwakeSettings(),new(new FakeExecutionState()));
        ModuleRegistry registry=new(); registry.Register(module);
        Assert.AreEqual("keep-awake",module.Id); Assert.AreEqual("保持唤醒",module.DisplayName); Assert.AreEqual("Keep Awake",module.EnglishName);
        Assert.AreEqual("效率工具",module.Category); Assert.AreEqual("Productivity Tools",module.EnglishCategory); Assert.AreSame(module,registry.Find(module.Id));
    }
    [TestMethod] public void DefaultInactiveDoesNotCallWindows()
    {
        FakeExecutionState fake=new(); using KeepAwakeService service=new(fake);
        Assert.AreEqual(KeepAwakeState.Inactive,service.Snapshot.State); Assert.IsFalse(service.Snapshot.IsBusy); Assert.AreEqual(0,fake.Calls.Count);
    }
    [DataTestMethod][DataRow(KeepAwakeMode.System,0x80000001u)][DataRow(KeepAwakeMode.SystemAndDisplay,0x80000003u)]
    public async Task ModesUseOnlyRequiredFlagsOnTheSameThread(KeepAwakeMode mode,uint flags)
    {
        FakeExecutionState fake=new(); using KeepAwakeService service=new(fake);
        Assert.IsTrue(await service.StartAsync(mode,null)); await service.StopAsync();
        Assert.AreEqual(flags,(uint)fake.Calls.First().Flags); Assert.AreEqual(ExecutionState.Continuous,fake.Calls.Last().Flags);
        Assert.AreEqual(2,fake.Calls.Count); Assert.AreEqual(1,fake.Calls.Select(c=>c.Thread.ManagedThreadId).Distinct().Count());
        Assert.IsFalse(fake.Calls.Last().Thread.IsAlive); Assert.AreEqual(KeepAwakeState.Inactive,service.Snapshot.State);
    }
    [DataTestMethod][DataRow(15)][DataRow(30)][DataRow(60)][DataRow(120)][DataRow(240)][DataRow(0)]
    public async Task AllDurationPresetsStartWithExpectedExpiry(int minutes)
    {
        AwakeClock clock=new(); using KeepAwakeService service=new(new FakeExecutionState(),clock);
        await service.StartAsync(KeepAwakeMode.System,minutes==0?null:TimeSpan.FromMinutes(minutes));
        KeepAwakeSnapshot state=service.Snapshot;
        Assert.AreEqual(KeepAwakeState.Active,state.State);
        Assert.AreEqual(minutes==0?null:clock.Utc+TimeSpan.FromMinutes(minutes),state.ExpiresAt);
        Assert.AreEqual(minutes==0?null:TimeSpan.FromMinutes(minutes),state.Remaining);
    }
    [TestMethod] public async Task MonotonicCountdownIgnoresWallClockJumps()
    {
        AwakeClock clock=new(); using KeepAwakeService service=new(new FakeExecutionState(),clock);
        await service.StartAsync(KeepAwakeMode.System,TimeSpan.FromMinutes(15)); clock.Utc+=TimeSpan.FromDays(30); clock.Advance(TimeSpan.FromMinutes(2));
        Assert.AreEqual(TimeSpan.FromMinutes(13),service.Snapshot.Remaining); clock.Utc-=TimeSpan.FromDays(60);
        Assert.AreEqual(TimeSpan.FromMinutes(13),service.Snapshot.Remaining);
    }
    [TestMethod] public async Task ExpirationReleasesWithoutViewOrUiTimer()
    {
        FakeExecutionState fake=new(); AwakeClock clock=new(); using KeepAwakeService service=new(fake,clock);
        await service.StartAsync(KeepAwakeMode.System,TimeSpan.FromMinutes(15)); clock.Advance(TimeSpan.FromMinutes(16));
        Assert.IsTrue(SpinWait.SpinUntil(()=>!service.Snapshot.IsBusy,TimeSpan.FromSeconds(3)));
        Assert.AreEqual(KeepAwakeEndReason.Expired,service.Snapshot.EndReason); Assert.AreEqual(ExecutionState.Continuous,fake.Calls.Last().Flags);
    }
    [TestMethod] public async Task DisplayRefreshDoesNotRepeatNativeCalls()
    {
        FakeExecutionState fake=new(); using KeepAwakeService service=new(fake);
        KeepAwakeViewModel vm=new(service,new AwakeSettings()); await vm.StartAsync();
        for(int i=0;i<100;i++) vm.Refresh(); Assert.AreEqual(1,fake.Calls.Count);
    }
    [TestMethod] public async Task DuplicateStartDoesNotCreateAnotherWorker()
    {
        FakeExecutionState fake=new(); using KeepAwakeService service=new(fake);
        Task<bool>[] starts=Enumerable.Range(0,20).Select(_=>service.StartAsync(KeepAwakeMode.System,null)).ToArray();
        Assert.AreEqual(1,(await Task.WhenAll(starts)).Count(s=>s)); Assert.AreEqual(1,fake.Calls.Count);
    }
    [TestMethod] public async Task DisposeReleasesAndJoinsWorkerOnAppExit()
    {
        FakeExecutionState fake=new(); KeepAwakeService service=new(fake); await service.StartAsync(KeepAwakeMode.System,null);
        service.Dispose(); service.Dispose(); Assert.AreEqual(ExecutionState.Continuous,fake.Calls.Last().Flags);
        Assert.IsFalse(fake.Calls.Last().Thread.IsAlive); Assert.IsFalse(await service.StartAsync(KeepAwakeMode.System,null));
    }
    [TestMethod] public async Task TrayAndViewRecreationDoNotStopRequest()
    {
        FakeExecutionState fake=new(); using KeepAwakeModule module=new(new AwakeSettings(),new(fake));
        await ((KeepAwakeViewModel)module.CreateViewModel()).StartAsync();
        Assert.IsTrue(module.KeepInTray); Assert.AreSame(module.CreateViewModel(),module.CreateViewModel());
        Assert.AreEqual(KeepAwakeState.Active,module.Service.Snapshot.State); Assert.AreEqual(1,fake.Calls.Count);
    }
    [TestMethod] public async Task FailedActivationNeverShowsActive()
    {
        FakeExecutionState fake=new(){ActivationResult=new(0,5)}; using KeepAwakeService service=new(fake);
        Assert.IsFalse(await service.StartAsync(KeepAwakeMode.System,null));
        Assert.AreEqual(KeepAwakeState.Inactive,service.Snapshot.State); Assert.AreEqual(KeepAwakeEndReason.ActivationFailed,service.Snapshot.EndReason);
        Assert.AreEqual(5,service.Snapshot.Win32Error); Assert.AreEqual(ExecutionState.Continuous,fake.Calls.Last().Flags);
    }
    [TestMethod] public async Task ZeroWithNoLastErrorStillMeansFailure()
    {
        using KeepAwakeService service=new(new FakeExecutionState(){ActivationResult=new(0,0)});
        Assert.IsFalse(await service.StartAsync(KeepAwakeMode.System,null)); Assert.AreEqual(KeepAwakeEndReason.ActivationFailed,service.Snapshot.EndReason);
    }
    [TestMethod] public async Task NativeExceptionStillRunsRelease()
    {
        FakeExecutionState fake=new(){OnActivation=()=>throw new System.ComponentModel.Win32Exception(5)};
        using KeepAwakeService service=new(fake); Assert.IsFalse(await service.StartAsync(KeepAwakeMode.System,null));
        Assert.AreEqual(ExecutionState.Continuous,fake.Calls.Last().Flags); Assert.IsFalse(service.Snapshot.IsBusy);
    }
    [TestMethod] public async Task FailedReleaseExitsOwnerThreadAndReportsFailure()
    {
        FakeExecutionState fake=new(){ReleaseResult=new(0,5)}; using KeepAwakeService service=new(fake);
        await service.StartAsync(KeepAwakeMode.System,null); await service.StopAsync();
        Assert.AreEqual(KeepAwakeEndReason.ReleaseFailed,service.Snapshot.EndReason); Assert.IsFalse(fake.Calls.Last().Thread.IsAlive);
    }
    [TestMethod] public async Task StopDuringActivationIsSafe()
    {
        using ManualResetEventSlim entered=new(),resume=new();
        FakeExecutionState fake=new(){OnActivation=()=>{entered.Set();if(!resume.Wait(TimeSpan.FromSeconds(5)))throw new TimeoutException();}};
        using KeepAwakeService service=new(fake); Task<bool> start=service.StartAsync(KeepAwakeMode.System,null);
        Assert.IsTrue(entered.Wait(TimeSpan.FromSeconds(5))); Task stop=service.StopAsync(); Assert.AreEqual(KeepAwakeState.Stopping,service.Snapshot.State);
        resume.Set(); await Task.WhenAll(start,stop); Assert.IsFalse(service.Snapshot.IsBusy); Assert.IsFalse(fake.Calls.Last().Thread.IsAlive);
    }
    [TestMethod] public async Task CanRestartAfterStop()
    {
        FakeExecutionState fake=new(); using KeepAwakeService service=new(fake);
        await service.StartAsync(KeepAwakeMode.System,null); await service.StopAsync();
        Assert.IsTrue(await service.StartAsync(KeepAwakeMode.SystemAndDisplay,TimeSpan.FromMinutes(15))); await service.StopAsync(); Assert.AreEqual(4,fake.Calls.Count);
    }
    [TestMethod] public async Task InactiveStopIsIdempotent() { FakeExecutionState fake=new(); using KeepAwakeService service=new(fake); await service.StopAsync(); await service.StopAsync(); Assert.AreEqual(0,fake.Calls.Count); }
    [TestMethod] public void InvalidInputsAreRejectedBeforeNativeCalls()
    {
        FakeExecutionState fake=new(); using KeepAwakeService service=new(fake);
        Assert.ThrowsException<ArgumentOutOfRangeException>(()=>service.StartAsync((KeepAwakeMode)3,null));
        Assert.ThrowsException<ArgumentOutOfRangeException>(()=>service.StartAsync(KeepAwakeMode.System,TimeSpan.Zero));
        Assert.ThrowsException<ArgumentOutOfRangeException>(()=>service.StartAsync(KeepAwakeMode.System,TimeSpan.FromDays(2))); Assert.AreEqual(0,fake.Calls.Count);
    }
    [TestMethod] public async Task PreferencesPersistButActiveStateDoesNot()
    {
        AwakeSettings settings=new(); using KeepAwakeService service=new(new FakeExecutionState()); KeepAwakeViewModel vm=new(service,settings);
        vm.SelectedMode=1; vm.SelectedDuration=vm.Durations.Single(d=>d.Minutes==120); await vm.StartAsync();
        AppSettings restored=JsonSerializer.Deserialize<AppSettings>(settings.Json)!;
        Assert.AreEqual(1,restored.KeepAwakeLastMode); Assert.AreEqual(120,restored.KeepAwakeLastDurationMinutes);
        Assert.IsFalse(settings.Json.Contains("KeepAwakeActive"));
        using KeepAwakeService restarted=new(new FakeExecutionState()); KeepAwakeViewModel restoredVm=new(restarted,new AwakeSettings(){Settings=restored});
        Assert.AreEqual(1,restoredVm.SelectedMode); Assert.AreEqual(120,restoredVm.SelectedDuration.Minutes); Assert.IsFalse(restoredVm.IsActive);
    }
    [TestMethod] public void InvalidSavedPreferencesFallBackSafely()
    {
        using KeepAwakeService service=new(new FakeExecutionState()); KeepAwakeViewModel vm=new(service,new AwakeSettings(){Settings=new(){KeepAwakeLastMode=99,KeepAwakeLastDurationMinutes=-1}});
        Assert.AreEqual(0,vm.SelectedMode); Assert.AreEqual(30,vm.SelectedDuration.Minutes); Assert.IsFalse(vm.IsActive);
    }
    [TestMethod] public async Task ActiveConfigurationIsDisabledButStopNeedsNoConfirmation()
    {
        using KeepAwakeService service=new(new FakeExecutionState()); KeepAwakeViewModel vm=new(service,new AwakeSettings()); await vm.StartAsync();
        Assert.IsFalse(vm.CanConfigure); Assert.IsFalse(vm.StartCommand.CanExecute(null)); Assert.IsTrue(vm.StopCommand.CanExecute(null));
        vm.SelectedMode=1; Assert.AreEqual(0,vm.SelectedMode); await vm.StopAsync(); Assert.IsTrue(vm.CanConfigure);
    }
    [DataTestMethod][DataRow(6156,"01:42:36")][DataRow(0,"00:00:00")][DataRow(-2,"00:00:00")]
    public void CountdownFormatting(int seconds,string expected)=>Assert.AreEqual(expected,KeepAwakeViewModel.FormatRemaining(TimeSpan.FromSeconds(seconds)));
    [TestMethod] public void IndefiniteCountdownIsExplicit()=>StringAssert.Contains(KeepAwakeViewModel.FormatRemaining(null),"Until stopped");
    [TestMethod] public void DurationSelectionUsesLabelInExistingComboBoxTemplate()=>Assert.AreEqual("30 minutes",new DurationOption(30,"30 minutes").ToString());
}

internal sealed class FakeExecutionState : IExecutionStatePlatform
{
    public ConcurrentQueue<(ExecutionState Flags,Thread Thread)> Calls {get;}=new();
    public ExecutionStateResult ActivationResult {get;set;}=new(0x80000000,0);
    public ExecutionStateResult ReleaseResult {get;set;}=new(0x80000001,0);
    public Action? OnActivation;
    public ExecutionStateResult Set(ExecutionState flags)
    {
        Calls.Enqueue((flags,Thread.CurrentThread));
        if(flags==ExecutionState.Continuous)return ReleaseResult;
        OnActivation?.Invoke(); return ActivationResult;
    }
}
internal sealed class AwakeClock : TimeProvider
{
    private long _ticks;
    public DateTimeOffset Utc=new(2026,1,1,0,0,0,TimeSpan.Zero);
    public override DateTimeOffset GetUtcNow()=>Utc;
    public override long TimestampFrequency=>TimeSpan.TicksPerSecond;
    public override long GetTimestamp()=>Interlocked.Read(ref _ticks);
    public void Advance(TimeSpan elapsed)=>Interlocked.Add(ref _ticks,elapsed.Ticks);
}
internal sealed class AwakeSettings : ISettingsService
{
    public AppSettings Settings {get;set;}=new();
    public string SettingsFilePath=>"unused";
    public string Json="";
    public Task LoadAsync()=>Task.CompletedTask;
    public Task SaveAsync(){Json=JsonSerializer.Serialize(Settings);return Task.CompletedTask;}
}
