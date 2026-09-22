using System.Text.RegularExpressions;

namespace WindowsToolbox.Tests;

[TestClass]
public sealed class ToolSafetyTests
{
    [DataTestMethod][DataRow("LockInspector")][DataRow("KeepAwake")]
    public void NewModulesHaveNoDestructiveOrElevatedCapabilities(string module)
    {
        string source=ModuleSource(module);
        foreach(string forbidden in new[]{"RmShutdown","RmRestart","TerminateProcess",".Kill(","taskkill","Stop-Process","NtQuerySystemInformation","DuplicateHandle","CM_Request_Device_Eject","CM_Query_And_Remove_SubTree","\"runas\"","SendInput","powercfg","Registry.","HttpClient","WebClient"})
            Assert.IsFalse(source.Contains(forbidden,StringComparison.OrdinalIgnoreCase),$"Forbidden capability: {forbidden}");
    }
    [TestMethod] public void RestartManagerInteropExposesOnlyFourDiagnosticCalls()
    {
        string source=File.ReadAllText(Path.Combine(ProjectRoot(),"src","WindowsToolbox.Modules.LockInspector","Interop","RestartManagerNative.cs"));
        string[] names=Regex.Matches(source,@"extern int (Rm\w+)\(").Select(m=>m.Groups[1].Value).Order().ToArray();
        CollectionAssert.AreEqual(new[]{"RmEndSession","RmGetList","RmRegisterResources","RmStartSession"},names);
    }
    [TestMethod] public void InspectorDoesNotReadFileContentsOrPersistTargets()
    {
        string source=ModuleSource("LockInspector");
        foreach(string forbidden in new[]{"FileStream","File.Open","ReadAllText","WriteAllText","Serialize","Trace.","ILogger"}) Assert.IsFalse(source.Contains(forbidden),forbidden);
    }
    [TestMethod] public void AwakeViewUnloadOnlyStopsDisplayTimerAndAppExitDisposesModule()
    {
        string view=File.ReadAllText(Path.Combine(ProjectRoot(),"src","WindowsToolbox.Modules.KeepAwake","Views","KeepAwakeView.xaml.cs"));
        StringAssert.Contains(view,"Unloaded+=(_,_)=>_displayTimer.Stop()");
        string app=File.ReadAllText(Path.Combine(ProjectRoot(),"src","WindowsToolbox.App","App.xaml.cs"));
        StringAssert.Contains(app,"bool keepAwake = _keepAwakeModule.KeepInTray;");
        StringAssert.Contains(app,"_keepAwakeModule?.Dispose();");
    }
    [TestMethod] public void NewModulesOnlyReferenceCore()
    {
        foreach(string module in new[]{"LockInspector","KeepAwake"})
        {
            string project=File.ReadAllText(Path.Combine(ProjectRoot(),"src",$"WindowsToolbox.Modules.{module}",$"WindowsToolbox.Modules.{module}.csproj"));
            Assert.AreEqual(1,Regex.Matches(project,"ProjectReference").Count); StringAssert.Contains(project,"WindowsToolbox.Core");
        }
    }
    private static string ModuleSource(string module)=>string.Join('\n',Directory.EnumerateFiles(Path.Combine(ProjectRoot(),"src",$"WindowsToolbox.Modules.{module}"),"*.cs",SearchOption.AllDirectories)
        .Where(p=>!p.Contains($"{Path.DirectorySeparatorChar}obj{Path.DirectorySeparatorChar}")&&!p.Contains($"{Path.DirectorySeparatorChar}bin{Path.DirectorySeparatorChar}")).Select(File.ReadAllText));
    private static string ProjectRoot()=>Path.GetFullPath(Path.Combine(AppContext.BaseDirectory,"..","..","..","..",".."));
}
