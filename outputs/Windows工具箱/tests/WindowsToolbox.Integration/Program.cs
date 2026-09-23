using System.Diagnostics;
using WindowsToolbox.Modules.LockInspector.Models;
using WindowsToolbox.Modules.LockInspector.Services;
using WindowsToolbox.Modules.KeepAwake.Interop;
using WindowsToolbox.Modules.KeepAwake.Models;
using WindowsToolbox.Modules.KeepAwake.Services;
using WindowsToolbox.Modules.LockInspector.Interop;
using WindowsToolbox.Modules.Utilities.QR.Models;
using WindowsToolbox.Modules.Utilities.QR.Services;

// Explicit opt-in executable; never run by dotnet test, never scans user files.
if (args.Length == 1 && args[0] == "--ui-smoke") { UiSmoke.Run(); return; }
if (args.Length == 1 && args[0] == "--qr-roundtrip")
{
    QrCodeService qr = new();
    foreach (string text in new[] { "ASCII round trip", "中文 QR 往返", "Emoji 😀 QR" })
    {
        var image = qr.Generate(text, 512, QrErrorCorrection.Medium, QrQuietZoneStyle.Standard);
        string? decoded = qr.Decode(image)?.Text;
        Check(string.Equals(decoded, text, StringComparison.Ordinal), $"QR round trip failed for a {text.Length}-character input");
    }
    Console.WriteLine("PASS: ZXing.Net QR encode -> WPF BitmapSource -> decode for ASCII, Chinese, and Emoji.");
    return;
}
if (args.Length == 1 && args[0] == "--screen-picker") { ScreenPickerIntegration.Run(); return; }
if (args.Length == 1 && args[0] == "--keep-awake")
{
    foreach (KeepAwakeMode mode in Enum.GetValues<KeepAwakeMode>())
    {
        using KeepAwakeService service = new(new WindowsExecutionStatePlatform());
        try { Check(await service.StartAsync(mode, null), "Native execution-state activation failed"); }
        finally { await service.StopAsync(); }
        Check(service.Snapshot.State == KeepAwakeState.Inactive && service.Snapshot.EndReason == KeepAwakeEndReason.Stopped, "Native execution-state release failed");
    }
    Console.WriteLine("PASS: both native execution-state modes immediately released on their owner thread.");
    return;
}
if (args.Length == 1 && args[0] == "--stress") { await StressCheck.RunAsync(); return; }
if (args.Length != 1 || args[0] != "--restart-manager")
    throw new ArgumentException("Explicit checks: --restart-manager, --keep-awake (immediate release), --stress (20k temp files + Fake RM), --ui-smoke (offscreen WPF rendering), --qr-roundtrip (real local QR codec), --screen-picker (samples a test window only).");
string root = Directory.CreateDirectory(Path.Combine(Path.GetTempPath(), "WindowsToolbox.LockInspector.Tests", Guid.NewGuid().ToString("N"))).FullName;
try
{
    string path = Path.Combine(root, "临时锁定-😀.txt");
    LockScanService scanner = new(new RestartManagerClient());
    using (FileStream held = new(path, FileMode.Create, FileAccess.ReadWrite, FileShare.None))
    {
        LockScanResult result = await scanner.ScanAsync(new([path]));
        Check(result.Errors.Count == 0, "Native RM returned an error");
        using Process self = Process.GetCurrentProcess();
        Check(result.Blockers.Any(b => b.ProcessId == Environment.ProcessId && b.ProcessStartTime == self.StartTime.ToFileTimeUtc()), "Temporary locked file did not identify this process");
    }
    LockScanResult unlocked = await scanner.ScanAsync(new([path]));
    Check(unlocked.Errors.Count == 0 && unlocked.Blockers.Count == 0, "Unlocked file has an unexpected blocker/error");
    // Repeated real sessions also exercise normal cleanup; fake tests cover exceptional cleanup.
    for (int i = 0; i < 10; i++) Check((await scanner.ScanAsync(new([path]))).Errors.Count == 0, "Repeated session failed");
    Console.WriteLine("PASS: real RM Unicode locked/unlocked temporary file, PID + start time, repeated sessions.");
    string second = Path.Combine(root,"second.txt");
    using (FileStream held = new(path,FileMode.Open,FileAccess.ReadWrite,FileShare.None))
    using (FileStream heldSecond = new(second,FileMode.Create,FileAccess.ReadWrite,FileShare.None))
    {
        LockScanResult multiple = await scanner.ScanAsync(new([path,second]));
        Check(multiple.Errors.Count==0 && multiple.FilesRegistered==2 && multiple.Blockers.Count(b=>b.ProcessId==Environment.ProcessId)==1,"Multi-file aggregation failed");
        LockScanResult folder = await scanner.ScanAsync(new([root],LockScanType.Folder,true));
        Check(folder.Errors.Count==0 && folder.Blockers.Any(b=>b.ProcessId==Environment.ProcessId),"Real folder scan failed");
    }
    using (CancellationTokenSource cancellation=new())
    {
        ObservedRm observed = new(){AfterRegister=cancellation.Cancel};
        LockScanResult cancelled = await new LockScanService(observed).ScanAsync(new([path]),cancellation.Token);
        Check(cancelled.WasCancelled && observed.Ends==1,"Cancelled real session was not closed");
        observed.AfterRegister=()=>throw new InvalidOperationException("Controlled failure after native registration");
        try { await new LockScanService(observed).ScanAsync(new([path])); throw new Exception("Expected controlled failure"); }
        catch(InvalidOperationException) { Check(observed.Ends==2,"Exceptional real session was not closed"); }
    }
    Console.WriteLine("PASS: real multi-file/folder aggregation and real session cleanup after cancellation/error.");
    string longDirectory=root;
    for(int i=0;i<4;i++) longDirectory=Directory.CreateDirectory(Path.Combine(longDirectory,new string('x',70))).FullName;
    string longPath=Path.Combine(longDirectory,"长路径-😀.txt");
    using(FileStream held=new(longPath,FileMode.Create,FileAccess.ReadWrite,FileShare.None))
    {
        LockScanResult longResult=await scanner.ScanAsync(new([longPath]));
        Console.WriteLine($"RM long-path observation: length={longPath.Length}; errors={string.Join(',',longResult.Errors)}; registered={longResult.FilesRegistered}; blockers={longResult.Blockers.Count}");
        if(longResult.Errors.Count==0)
        {
            Check(longResult.Blockers.Any(b=>b.ProcessId==Environment.ProcessId),"Native long path silently missed known lock");
            Console.WriteLine("PASS: native long-path lock detected.");
        }
        else
        {
            Check(longResult.FilesRegistered==0 && longResult.Errors.All(code=>code is 29 or 87 or 160 or 206),"Unexpected long-path failure");
            Check(RestartManagerErrors.Describe(longResult.RestartManagerError).Contains("路径"),"Missing readable path limitation");
            Console.WriteLine("PASS: native long path rejected explicitly; reported as unsupported/unavailable, never a clean no-blocker result.");
        }
        using FileStream normalHeld=new(path,FileMode.Open,FileAccess.ReadWrite,FileShare.None);
        LockScanResult mixed=await scanner.ScanAsync(new([longPath,path]));
        Check(mixed.Blockers.Any(b=>b.ProcessId==Environment.ProcessId),"Long-path rejection discarded normal file scan");
    }
}
finally { Directory.Delete(root, true); }

static void Check(bool condition, string message) { if (!condition) throw new InvalidOperationException(message); }

sealed class ObservedRm : IRestartManagerClient
{
    private readonly RestartManagerClient _native=new();
    public Action? AfterRegister;
    public int Ends;
    public int StartSession(out uint session)=>_native.StartSession(out session);
    public int RegisterResources(uint session,string[] files){int result=_native.RegisterResources(session,files);AfterRegister?.Invoke();return result;}
    public int GetList(uint session,out uint needed,ref uint count,RmProcessInfo[]? processes,out uint rebootReasons)=>_native.GetList(session,out needed,ref count,processes,out rebootReasons);
    public int EndSession(uint session){Ends++;return _native.EndSession(session);}
}
