using System.Runtime.InteropServices;
using WindowsToolbox.Core.Services;
using WindowsToolbox.Modules.LockInspector;
using WindowsToolbox.Modules.LockInspector.Interop;
using WindowsToolbox.Modules.LockInspector.Models;
using WindowsToolbox.Modules.LockInspector.Services;
using WindowsToolbox.Modules.LockInspector.ViewModels;

namespace WindowsToolbox.Tests;

[TestClass]
public sealed class LockInspectorTests
{
    private string _root = "";
    [TestInitialize] public void Setup() => _root = Directory.CreateDirectory(Path.Combine(Path.GetTempPath(), "WindowsToolbox.LockInspector.Tests", Guid.NewGuid().ToString("N"))).FullName;
    [TestCleanup] public void Cleanup() => Directory.Delete(_root, true);
    private string FileAt(string name = "文件-😀.txt") { string path = Path.Combine(_root, name); File.WriteAllText(path, ""); return path; }
    private Task<LockScanResult> Scan(FakeRestartManager fake, params string[] paths) => new LockScanService(fake).ScanAsync(new(paths));

    [TestMethod] public void MetadataAndRegistration()
    {
        LockInspectorModule module = new(); ModuleRegistry registry = new(); registry.Register(module);
        Assert.AreEqual("lock-inspector", module.Id); Assert.AreEqual("占用检测", module.DisplayName);
        Assert.AreEqual("Lock Inspector", module.EnglishName); Assert.AreEqual("效率工具", module.Category);
        Assert.AreEqual("Productivity Tools", module.EnglishCategory); Assert.AreSame(module, registry.Find(module.Id));
    }
    [TestMethod] public void NativeLayoutMatchesWindows() { Assert.AreEqual(12, Marshal.SizeOf<RmUniqueProcess>()); Assert.AreEqual(668, Marshal.SizeOf<RmProcessInfo>()); }
    [TestMethod] public async Task SingleUnicodeFileRegistersAndCleansSession()
    {
        FakeRestartManager fake = new(); string file = FileAt(); LockScanResult result = await Scan(fake, file);
        Assert.AreEqual(file, fake.Batches.Single().Single()); Assert.AreEqual(1, result.FilesRegistered);
        Assert.AreEqual(1, fake.Starts); Assert.AreEqual(1, fake.Ends); Assert.AreEqual(0, result.Blockers.Count);
    }
    [TestMethod] public async Task MultipleFilesRegisterTogether()
    {
        FakeRestartManager fake = new(); LockScanResult result = await Scan(fake, FileAt("a"), FileAt("b"));
        Assert.AreEqual(2, fake.Batches.Single().Length); Assert.AreEqual(2, result.FilesRegistered);
    }
    [TestMethod] public async Task DuplicatePathsAreRemoved() { FakeRestartManager fake = new(); string file = FileAt(); await Scan(fake, file, file.ToUpperInvariant()); Assert.AreEqual(1, fake.Batches.Single().Length); }
    [TestMethod] public async Task InvalidMissingAndDirectoryInputsAreSkipped()
    {
        FakeRestartManager fake = new(); LockScanResult result = await Scan(fake, "relative.txt", "", Path.Combine(_root,"gone"), _root);
        Assert.AreEqual(4, result.FilesSkipped); Assert.AreEqual(0, fake.Starts);
    }
    [TestMethod] public async Task BatchSizeIsBounded()
    {
        FakeRestartManager fake = new(); await Scan(fake, Enumerable.Range(0, 257).Select(i => FileAt(i.ToString())).ToArray());
        CollectionAssert.AreEqual(new[] {256, 1}, fake.Batches.Select(b => b.Length).ToArray()); Assert.AreEqual(2, fake.Ends);
    }
    [DataTestMethod][DataRow(5)][DataRow(6)][DataRow(14)][DataRow(121)][DataRow(160)][DataRow(234)][DataRow(206)]
    public async Task ApiErrorsAreReadableAndCleanupRuns(int error)
    {
        FakeRestartManager fake = new() { ListError = error }; LockScanResult result = await Scan(fake, FileAt());
        Assert.AreEqual(error, result.RestartManagerError); Assert.AreEqual(1, fake.Ends);
        Assert.IsTrue(RestartManagerErrors.Describe(error).Length > 20);
    }
    [TestMethod] public async Task FailedStartDoesNotEndUnownedSession() { FakeRestartManager fake = new() { StartError=121 }; Assert.AreEqual(121, (await Scan(fake, FileAt())).RestartManagerError); Assert.AreEqual(0, fake.Ends); }
    [TestMethod] public async Task FailedRegisterStillEndsSession() { FakeRestartManager fake = new() { RegisterError=5 }; Assert.AreEqual(5,(await Scan(fake, FileAt())).RestartManagerError); Assert.AreEqual(1, fake.Ends); }
    [TestMethod] public async Task FailedEndIsReported() { FakeRestartManager fake = new() { EndError=6 }; Assert.AreEqual(6,(await Scan(fake, FileAt())).RestartManagerError); }
    [TestMethod] public async Task MoreDataRetriesAndRetainsIdentity()
    {
        FakeRestartManager fake = new() { Processes=[FakeRestartManager.Process(77,123456789)], AdditionalMoreData=1 };
        LockScanResult result=await Scan(fake,FileAt());
        Assert.AreEqual(3,fake.ListCalls); Assert.AreEqual(77,result.Blockers.Single().ProcessId); Assert.AreEqual(123456789L,result.Blockers.Single().ProcessStartTime);
    }
    [TestMethod] public async Task MoreDataRetryIsBounded() { FakeRestartManager fake=new(){ ListError=234 }; await Scan(fake,FileAt()); Assert.AreEqual(4,fake.ListCalls); Assert.AreEqual(1,fake.Ends); }
    [TestMethod] public async Task ApplicationAndServiceMetadataArePreserved()
    {
        RmProcessInfo service = FakeRestartManager.Process(88,42); service.ServiceShortName="svc"; service.ApplicationType=RmApplicationType.Service; service.Restartable=true;
        FakeRestartManager fake=new(){ Processes=[FakeRestartManager.Process(77,43),service] };
        LockScanResult result=await Scan(fake,FileAt()); Assert.AreEqual(2,result.Blockers.Count);
        LockingProcessInfo blocker=result.Blockers.Single(b=>b.ProcessId==88);
        Assert.AreEqual("svc",blocker.ServiceShortName); Assert.IsTrue(blocker.Restartable); Assert.IsNull(blocker.ExecutablePath);
    }
    [TestMethod] public async Task SamePidDifferentStartTimesDoNotMerge()
    {
        FakeRestartManager fake=new(){ Processes=[FakeRestartManager.Process(77,1),FakeRestartManager.Process(77,2),FakeRestartManager.Process(77,1)] };
        Assert.AreEqual(2,(await Scan(fake,FileAt())).Blockers.Count);
    }
    [TestMethod] public async Task CancelBeforeStartOpensNoSession()
    {
        FakeRestartManager fake=new(); using CancellationTokenSource source=new(); source.Cancel();
        LockScanResult result=await new LockScanService(fake).ScanAsync(new([FileAt()]),source.Token);
        Assert.IsTrue(result.WasCancelled); Assert.AreEqual(0,fake.Starts);
    }
    [TestMethod] public async Task CancelDuringRegisterEndsSession()
    {
        using CancellationTokenSource source=new(); FakeRestartManager fake=new(){ OnRegister=source.Cancel };
        LockScanResult result=await new LockScanService(fake).ScanAsync(new([FileAt()]),source.Token);
        Assert.IsTrue(result.WasCancelled); Assert.AreEqual(1,fake.Ends);
    }
    [TestMethod] public async Task ExceptionStillEndsSession()
    {
        FakeRestartManager fake=new(){ OnRegister=()=>throw new InvalidOperationException("fake") };
        await Assert.ThrowsExceptionAsync<InvalidOperationException>(()=>Scan(fake,FileAt())); Assert.AreEqual(1,fake.Ends);
    }
    [TestMethod] public async Task FolderQuickScanStaysAtRoot()
    {
        FileAt("root"); Directory.CreateDirectory(Path.Combine(_root,"child")); FileAt("child/nested");
        FakeRestartManager fake=new(); LockScanResult result=await new LockScanService(fake).ScanAsync(new([_root],LockScanType.Folder));
        Assert.AreEqual(1,result.FilesRegistered); Assert.AreEqual(1,fake.Batches.Count);
    }
    [TestMethod] public async Task FolderRecursiveScanIncludesNestedFiles()
    {
        FileAt("root"); Directory.CreateDirectory(Path.Combine(_root,"child")); FileAt("child/nested");
        FakeRestartManager fake=new(); LockScanResult result=await new LockScanService(fake).ScanAsync(new([_root],LockScanType.Folder,true));
        Assert.AreEqual(2,result.FilesRegistered); Assert.AreEqual(2,fake.Batches.Single().Length);
    }
    [TestMethod] public async Task MissingFolderDoesNotDiscardOtherTargets()
    {
        FileAt(); FakeRestartManager fake=new(); LockScanResult result=await new LockScanService(fake).ScanAsync(new([Path.Combine(_root,"gone"),_root],LockScanType.Folder,true));
        Assert.AreEqual(1,result.FilesRegistered); Assert.IsTrue(result.FilesSkipped>0);
    }
    [TestMethod] public async Task NetworkFolderIsSkippedWithoutEnumeration()
    {
        FakeRestartManager fake=new(); LockScanResult result=await new LockScanService(fake).ScanAsync(new([@"\\invalid.invalid\share"],LockScanType.Folder,true));
        Assert.AreEqual(1,result.FilesSkipped); Assert.AreEqual(0,fake.Starts);
    }
    [TestMethod] public async Task QuickLimitIsReported()
    {
        for(int i=0;i<5001;i++) FileAt(i.ToString());
        FakeRestartManager fake=new(); LockScanResult result=await new LockScanService(fake).ScanAsync(new([_root],LockScanType.Folder));
        Assert.AreEqual(5000,result.FilesRegistered); Assert.IsTrue(result.WasLimited);
        Assert.IsTrue(fake.Batches.All(b=>b.Length<=256)); StringAssert.Contains(LockInspectorViewModel.Describe(result),"上限");
    }
    [TestMethod] public async Task EnumerationIsStreamingAndCancellableBetweenBatches()
    {
        for(int i=0;i<600;i++) FileAt(i.ToString());
        using CancellationTokenSource source=new(); FakeRestartManager fake=new(){ OnRegister=source.Cancel };
        LockScanResult result=await new LockScanService(fake).ScanAsync(new([_root],LockScanType.Folder,true),source.Token);
        Assert.IsTrue(result.WasCancelled); Assert.AreEqual(256,result.FilesEnumerated); Assert.AreEqual(1,fake.Ends);
    }
    [TestMethod] public async Task AggregatesAcrossBatchesByProcessIdentity()
    {
        FakeRestartManager fake=new(){Processes=[FakeRestartManager.Process(5,6)]};
        LockScanResult result=await Scan(fake,Enumerable.Range(0,257).Select(i=>FileAt(i.ToString())).ToArray());
        Assert.AreEqual(1,result.Blockers.Count); Assert.AreEqual(2,result.Batches);
    }
    [TestMethod] public async Task LongPathsAreNotTruncatedOrMixedWithNormalFiles()
    {
        string relative=string.Join('/',Enumerable.Repeat(new string('x',70),4)); Directory.CreateDirectory(Path.Combine(_root,relative));
        string longPath=FileAt(relative+"/中文.txt"); FakeRestartManager fake=new();
        await Scan(fake,FileAt("normal"),longPath,FileAt("normal2"));
        Assert.AreEqual(Path.GetFullPath(longPath),fake.Batches[1].Single()); Assert.AreEqual(3,fake.Batches.Count);
    }
    [DataTestMethod][DataRow(DriveType.Removable,true)][DataRow(DriveType.Fixed,false)]
    public void DriveLabelDoesNotGuessBusType(DriveType type,bool removable)=>Assert.AreEqual(removable,new DriveTarget(@"X:\",type).Label.Contains("可移动"));
    [TestMethod] public async Task DriveEmptyResultNeverPromisesSafeEject()
    {
        LockScanResult result=await new LockScanService(new FakeRestartManager()).ScanAsync(new([_root],LockScanType.Drive));
        StringAssert.Contains(LockInspectorViewModel.Describe(result),"仍可能"); Assert.AreEqual(LockScanType.Drive,result.Target.ScanType);
    }
    [TestMethod] public void StaleIdentityIsRejected()
    {
        LockingProcessInfo info=FakeRestartManager.Process(5,123).ToModel(); Assert.IsTrue(ProcessDetailsService.Matches(info,5,123));
        Assert.IsFalse(ProcessDetailsService.Matches(info,5,124)); Assert.IsFalse(ProcessDetailsService.Matches(info,6,123));
    }
    [TestMethod] public void CopyContainsMetadataAndMissingPathCannotOpen()
    {
        LockingProcessInfo info=FakeRestartManager.Process(5,123).ToModel(); Assert.IsFalse(ProcessDetailsService.CanOpenLocation(info));
        StringAssert.Contains(info.InfoText,"PID 5"); StringAssert.Contains(info.InfoText,"123");
        Assert.IsTrue(ProcessDetailsService.CanOpenLocation(info with{ExecutablePath=FileAt()}));
    }
    [TestMethod] public async Task UiIdleNoBlockerAndRescanStates()
    {
        using LockInspectorViewModel vm=new(new LockScanService(new FakeRestartManager()));
        Assert.AreEqual(LockScanState.Idle,vm.State); Assert.IsFalse(vm.CanRescan);
        await vm.ScanAsync(new([FileAt()])); Assert.AreEqual(LockScanState.NoBlocker,vm.State); Assert.IsTrue(vm.CanRescan);
        await vm.RescanAsync(); Assert.AreEqual(LockScanState.NoBlocker,vm.State);
    }
    [TestMethod] public async Task UiFoundFailedAndLimitedStates()
    {
        FakeRestartManager fake=new(){Processes=[FakeRestartManager.Process(5,6)]};
        using LockInspectorViewModel vm=new(new LockScanService(fake)); await vm.ScanAsync(new([FileAt()]));
        Assert.AreEqual(LockScanState.Found,vm.State); Assert.IsTrue(vm.CanCopy);
        fake.ListError=5; await vm.RescanAsync(); Assert.AreEqual(LockScanState.Failed,vm.State);
        fake.ListError=0; await vm.ScanAsync(new([Path.Combine(_root,"missing")])); Assert.AreEqual(LockScanState.Limited,vm.State);
    }
    [TestMethod] public async Task NewScanCancelsAndWaitsForOldWithoutStaleOverwrite()
    {
        using ManualResetEventSlim entered=new(), resume=new();
        FakeRestartManager fake=new(){OnRegister=()=>{entered.Set(); if(!resume.Wait(TimeSpan.FromSeconds(5))) throw new TimeoutException();}};
        using LockInspectorViewModel vm=new(new LockScanService(fake));
        Task first=vm.ScanAsync(new([FileAt("old")])); Assert.IsTrue(entered.Wait(TimeSpan.FromSeconds(5)));
        Assert.IsTrue(vm.IsScanning); Assert.IsFalse(vm.CanSelect);
        Task second=vm.ScanAsync(new([FileAt("new")])); fake.OnRegister=null; resume.Set(); await Task.WhenAll(first,second);
        Assert.AreEqual(2,fake.Ends); StringAssert.EndsWith(vm.TargetText,"new"); Assert.AreEqual(LockScanState.NoBlocker,vm.State);
    }
    [TestMethod] public async Task CancelRestoresUiAndAllowsNextScan()
    {
        using LockInspectorViewModel vm=new(new LockScanService(new FakeRestartManager()));
        Task scan=vm.ScanAsync(new([FileAt()])); vm.Cancel(); await scan;
        Assert.AreEqual(LockScanState.Cancelled,vm.State); Assert.IsTrue(vm.CanSelect);
        await vm.RescanAsync(); Assert.AreEqual(LockScanState.NoBlocker,vm.State);
    }
    [TestMethod] public async Task DisposeClearsInMemoryTargetsAndResults()
    {
        LockInspectorViewModel vm=new(new LockScanService(new FakeRestartManager())); await vm.ScanAsync(new([FileAt()]));
        vm.Dispose(); Assert.IsNull(vm.Result); Assert.AreEqual(0,vm.Blockers.Count); Assert.IsFalse(vm.TargetText.Contains(_root));
    }
}

internal sealed class FakeRestartManager : IRestartManagerClient
{
    public int Starts, Ends, ListCalls, StartError, RegisterError, ListError, EndError, AdditionalMoreData;
    public Action? OnRegister;
    public List<string[]> Batches { get; }=[];
    public RmProcessInfo[] Processes { get; set; }=[];
    public int StartSession(out uint session) { Starts++; session=1; return StartError; }
    public int RegisterResources(uint session,string[] files) { Batches.Add(files); OnRegister?.Invoke(); return RegisterError; }
    public int EndSession(uint session) { Ends++; return EndError; }
    public int GetList(uint session,out uint needed,ref uint count,RmProcessInfo[]? processes,out uint rebootReasons)
    {
        ListCalls++; needed=(uint)Math.Max(1,Processes.Length); rebootReasons=0;
        if(ListError!=0) return ListError;
        if(Processes.Length>0 && (processes is null || AdditionalMoreData-- > 0)) return 234;
        count=(uint)Processes.Length; Processes.CopyTo(processes ?? [],0); return 0;
    }
    public static RmProcessInfo Process(int pid,long start)=>new()
    {
        Process=new(){ProcessId=pid,StartTime=new(){dwLowDateTime=(int)start,dwHighDateTime=(int)(start>>32)}},
        ApplicationName="Test application", ServiceShortName="",ApplicationType=RmApplicationType.MainWindow
    };
}
