using WindowsToolbox.Modules.LockInspector.Interop;

namespace WindowsToolbox.Modules.LockInspector.Services;

// This boundary is replaced by a Fake in every ordinary test.
public interface IRestartManagerClient
{
    int StartSession(out uint session);
    int RegisterResources(uint session, string[] files);
    int GetList(uint session, out uint needed, ref uint count, RmProcessInfo[]? processes, out uint rebootReasons);
    int EndSession(uint session);
}

public sealed class RestartManagerClient : IRestartManagerClient
{
    public int StartSession(out uint session) => RestartManagerNative.RmStartSession(out session, 0, new System.Text.StringBuilder(33));
    public int RegisterResources(uint session, string[] files) =>
        RestartManagerNative.RmRegisterResources(session, (uint)files.Length, files, 0, IntPtr.Zero, 0, IntPtr.Zero);
    public int GetList(uint session, out uint needed, ref uint count, RmProcessInfo[]? processes, out uint rebootReasons) =>
        RestartManagerNative.RmGetList(session, out needed, ref count, processes, out rebootReasons);
    public int EndSession(uint session) => RestartManagerNative.RmEndSession(session);
}
