using System.IO;

namespace WindowsToolbox.Modules.LockInspector.Models;

public sealed record DriveTarget(string Root, DriveType Type)
{
    public string Label => $"{Root} · {Type}" + (Type == DriveType.Removable ? " / 可移动" : "");
    public override string ToString() => Label;
    public static IReadOnlyList<DriveTarget> GetReadyDrives()
    {
        List<DriveTarget> result = [];
        foreach (DriveInfo drive in DriveInfo.GetDrives())
        {
            try { if (drive.DriveType != DriveType.Network && drive.IsReady) result.Add(new(drive.Name, drive.DriveType)); }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException) { }
        }
        return result;
    }
}
