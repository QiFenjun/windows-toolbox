using System.Diagnostics;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using WindowsToolbox.Modules.LockInspector.Models;
using WindowsToolbox.Modules.LockInspector.Services;
using WindowsToolbox.Modules.KeepAwake.Interop;
using WindowsToolbox.Modules.KeepAwake.Models;
using WindowsToolbox.Modules.KeepAwake.Services;
using WindowsToolbox.Modules.LockInspector.Interop;
using WindowsToolbox.Modules.Utilities.QR.Models;
using WindowsToolbox.Modules.Utilities.QR.Services;
using WindowsToolbox.Modules.Utilities.Random.Models;
using WindowsToolbox.Modules.Utilities.Random.Services;
using WindowsToolbox.Modules.Utilities.Image.Models;
using WindowsToolbox.Modules.Utilities.Image.Services;
using WindowsToolbox.Modules.Utilities.Regex.Models;
using WindowsToolbox.Modules.Utilities.Regex.Services;

// Explicit opt-in executable; never run by dotnet test, never scans user files.
if (args.Length >= 1 && args[0] == "--ui-smoke")
{
    string language = args.Length == 1 ? "zh-CN" : args.Length == 2 ? args[1] : string.Empty;
    if (language is not ("zh-CN" or "en-US"))
        throw new ArgumentException("Usage: --ui-smoke [zh-CN|en-US]");
    UiSmoke.Run(language);
    return;
}
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
if (args.Length == 1 && args[0] == "--secure-rng")
{
    RandomToolsService random = new(new SystemSecureRandomSource());
    var uuids = random.GenerateUuids(100);
    var strings = random.GenerateStrings(100, 64, new RandomStringOptions(Symbols: true));
    var integers = random.GenerateIntegers(-100, 100, 100);
    Check(uuids.All(value => value.Length == 36 && value[14] == '4' && "89ab".Contains(value[19])), "Production UUID output format is invalid");
    Check(strings.All(value => value.Length == 64), "Production random-string length is invalid");
    Check(integers.All(value => value is >= -100 and <= 100), "Production random-integer range is invalid");
    Stopwatch performance = Stopwatch.StartNew();
    var performanceStrings = random.GenerateStrings(1000, 128, new RandomStringOptions());
    performance.Stop();
    Check(performanceStrings.Count == 1000 && performanceStrings.All(value => value.Length == 128), "Maximum routine string batch failed");
    Check(performance.Elapsed < TimeSpan.FromSeconds(30), "1000 × 128 character production string generation exceeded 30 seconds");
    Console.WriteLine($"PASS: production RandomNumberGenerator API generated 100 UUIDs, strings, and bounded integers plus 1000 × 128 strings in {performance.ElapsedMilliseconds} ms; values were not logged. This smoke check is not a statistical security test.");
    return;
}
if (args.Length == 1 && args[0] == "--image-smoke")
{
    string fixtureRoot = Directory.CreateDirectory(Path.Combine(Path.GetTempPath(), "WindowsToolbox.ImageTools.Integration", Guid.NewGuid().ToString("N"))).FullName;
    try
    {
        var outcome = await ImageToolsService.OnWorkerAsync(() =>
        {
            string png = Path.Combine(fixtureRoot, "synthetic.png");
            byte[] pixels = new byte[64 * 32 * 4];
            for (int i = 0; i < 64 * 32; i++) { pixels[i * 4] = 0; pixels[i * 4 + 1] = 40; pixels[i * 4 + 2] = 220; pixels[i * 4 + 3] = i % 64 < 32 ? (byte)0 : (byte)255; }
            BitmapSource bitmap = BitmapSource.Create(64, 32, 96, 96, PixelFormats.Bgra32, null, pixels, 64 * 4);
            PngBitmapEncoder encoder = new(); encoder.Frames.Add(BitmapFrame.Create(bitmap)); using (FileStream file = File.Create(png)) encoder.Save(file);
            ImageInfo info = ImageToolsService.ReadInfo(png);
            Check(info.Format == "PNG" && info.Width == 64 && info.Height == 32, "PNG metadata mismatch");
            Check(ImageToolsService.LoadPreview(png).IsFrozen, "Preview was not frozen");
            string moved = png + ".rename-check"; File.Move(png, moved); File.Move(moved, png);
            ImageResult output = ImageToolsService.Process(png, new(16, 16, Format: ImageOutputFormat.Jpeg, JpegQuality: 85));
            Check(output.Status == ImageItemStatus.Succeeded && output.Output is not null, "PNG-to-JPEG resize failed");
            ImageInfo outputInfo = ImageToolsService.ReadInfo(output.Output!);
            Check(outputInfo.Format == "JPEG" && outputInfo.Width == 16 && outputInfo.Height == 8, "JPEG resize geometry or format mismatch");
            Check(File.Exists(png), "Source PNG was modified or removed");
            return (info, outputInfo);
        });
        Console.WriteLine($"PASS: generated temporary PNG {outcome.info.Width}×{outcome.info.Height}; read metadata, unlocked by rename after preview, resized/encoded JPEG {outcome.outputInfo.Width}×{outcome.outputInfo.Height}; source retained.");
    }
    finally { Directory.Delete(fixtureRoot, true); }
    return;
}
if (args.Length == 1 && args[0] == "--regex-smoke")
{
    RegexResult match = RegexToolsService.Run(new("(?<name>\\w+)-(\\d+)", "alpha-42", "${name}:$1"));
    Check(match.Status == RegexRunStatus.Matches && match.Replacement == "alpha:42" && match.Matches[0].Groups.Any(g => g.Name == "name" && g.Value == "alpha"), "Named group or replacement smoke failed");
    RegexResult timeout = RegexToolsService.Run(new("(a+)+$", new string('a', 80) + "!", TimeoutMilliseconds: 100));
    Check(timeout.Status == RegexRunStatus.TimedOut, "Catastrophic regex did not time out");
    Console.WriteLine("PASS: local .NET Regex named group/replacement and catastrophic backtracking timeout; no input values logged.");
    return;
}
if (args.Length == 1 && args[0] == "--screen-picker") { ScreenPickerIntegration.Run(); return; }
if (args.Length == 1 && args[0] == "--navigation-stress") { NavigationStress.Run(); return; }
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
    throw new ArgumentException("Explicit checks: --restart-manager, --keep-awake (immediate release), --stress (20k temp files + Fake RM), --ui-smoke (offscreen WPF rendering), --navigation-stress (offscreen WPF navigation + Dispatcher heartbeat), --image-smoke (synthetic local fixture), --regex-smoke (synthetic safe timeout case), --qr-roundtrip (real local QR codec), --secure-rng (real local CSPRNG smoke), --screen-picker (samples a test window only).");
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
