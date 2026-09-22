using System.Diagnostics;
using System.Security.AccessControl;
using System.Security.Principal;
using WindowsToolbox.Modules.LockInspector.Interop;
using WindowsToolbox.Modules.LockInspector.Models;
using WindowsToolbox.Modules.LockInspector.Services;

internal static class StressCheck
{
    internal static async Task RunAsync()
    {
        string root=Directory.CreateDirectory(Path.Combine(Path.GetTempPath(),"WindowsToolbox.LockInspector.Tests",Guid.NewGuid().ToString("N"))).FullName;
        string? junction=null;
        try
        {
            for(int i=0;i<20000;i++) using(File.Create(Path.Combine(root,$"{i:D5}.tmp"))) { }
            CountingRm fake=new(); LockScanService scanner=new(fake);
            long before=GC.GetTotalAllocatedBytes(true); Stopwatch watch=Stopwatch.StartNew();
            LockScanResult result=await scanner.ScanAsync(new([root],LockScanType.Folder,true)); watch.Stop();
            long allocated=GC.GetTotalAllocatedBytes(true)-before;
            Require(result.FilesRegistered==20000 && result.WasLimited,"20k limit failed");
            Require(fake.Starts==79 && fake.Starts==fake.Ends && fake.MaxBatch==256,"Batch/session bounds failed");
            Require(allocated<64*1024*1024,"Unexpected allocation growth");
            using Process self=Process.GetCurrentProcess();
            Console.WriteLine($"PASS: 20,000 empty files; {fake.Starts} batches; {watch.ElapsedMilliseconds} ms; allocated {allocated} bytes; process peak working set {self.PeakWorkingSet64} bytes.");
            using CancellationTokenSource cancel=new(); fake.OnRegister=cancel.Cancel;
            LockScanResult cancelled=await scanner.ScanAsync(new([root],LockScanType.Folder,true),cancel.Token);
            Require(cancelled.WasCancelled && cancelled.FilesEnumerated==256 && fake.Starts==fake.Ends,"Cancellation/session cleanup failed");
            Console.WriteLine("PASS: cancellation after one batch; no further enumeration; session released.");

            string tiny=Directory.CreateDirectory(Path.Combine(root,"tiny")).FullName;
            using(File.Create(Path.Combine(tiny,"one.txt"))) { }
            junction=Path.Combine(tiny,"cycle");
            // Own temporary paths only. Junctions do not require elevation or developer mode.
            ProcessStartInfo link=new(Path.Combine(Environment.SystemDirectory,"cmd.exe")){UseShellExecute=false,CreateNoWindow=true,RedirectStandardOutput=true,RedirectStandardError=true};
            foreach(string arg in new[]{"/d","/c","mklink","/j",junction,tiny}) link.ArgumentList.Add(arg);
            using(Process process=Process.Start(link)!)
            {
                _=await process.StandardOutput.ReadToEndAsync(); _=await process.StandardError.ReadToEndAsync(); await process.WaitForExitAsync();
                Require(process.ExitCode==0,"Could not create temporary junction");
            }
            fake.OnRegister=null;
            LockScanResult reparse=await scanner.ScanAsync(new([tiny],LockScanType.Folder,true));
            Require(reparse.FilesRegistered==1 && reparse.FilesSkipped>0,"Reparse point followed or not reported");
            Console.WriteLine("PASS: junction cycle skipped without following target.");
            Directory.Delete(junction); junction=null;

            DirectoryInfo denied=Directory.CreateDirectory(Path.Combine(tiny,"denied"));
            using(File.Create(Path.Combine(denied.FullName,"hidden.txt"))) { }
            string original=denied.GetAccessControl().GetSecurityDescriptorSddlForm(AccessControlSections.Access);
            DirectorySecurity restricted=denied.GetAccessControl();
            SecurityIdentifier sid=WindowsIdentity.GetCurrent().User!;
            restricted.AddAccessRule(new FileSystemAccessRule(sid,FileSystemRights.ListDirectory,AccessControlType.Deny));
            try
            {
                denied.SetAccessControl(restricted);
                LockScanResult inaccessible=await scanner.ScanAsync(new([tiny],LockScanType.Folder,true));
                Require(inaccessible.FilesRegistered==1 && inaccessible.FilesSkipped>0,"Inaccessible folder was not skipped");
            }
            finally
            {
                DirectorySecurity restored=new();
                restored.SetSecurityDescriptorSddlForm(original,AccessControlSections.Access);
                denied.SetAccessControl(restored);
            }
            Console.WriteLine("PASS: access-denied temporary folder skipped; ACL restored.");
        }
        finally
        {
            if(junction is not null && Directory.Exists(junction)) Directory.Delete(junction);
            Directory.Delete(root,true);
        }
        Console.WriteLine("PASS: all stress fixtures cleaned.");
    }
    private static void Require(bool condition,string message){if(!condition)throw new InvalidOperationException(message);}
    private sealed class CountingRm : IRestartManagerClient
    {
        public int Starts,Ends,MaxBatch;
        public Action? OnRegister;
        public int StartSession(out uint session){session=(uint)++Starts;return 0;}
        public int EndSession(uint session){Ends++;return 0;}
        public int RegisterResources(uint session,string[] files){MaxBatch=Math.Max(MaxBatch,files.Length);OnRegister?.Invoke();return 0;}
        public int GetList(uint session,out uint needed,ref uint count,RmProcessInfo[]? processes,out uint rebootReasons){needed=0;count=0;rebootReasons=0;return 0;}
    }
}
