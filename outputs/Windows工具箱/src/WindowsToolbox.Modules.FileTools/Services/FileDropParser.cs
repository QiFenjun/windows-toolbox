using System.Windows;
using WpfDataObject = System.Windows.IDataObject;
using WpfDataFormats = System.Windows.DataFormats;

namespace WindowsToolbox.Modules.FileTools.Services;

public static class FileDropParser
{
    public static IReadOnlyList<string> Parse(WpfDataObject? data)
    {
        if (data is null || !data.GetDataPresent(WpfDataFormats.FileDrop, true))
            return [];

        return data.GetData(WpfDataFormats.FileDrop, true) is string[] paths
            ? paths.Where(path => !string.IsNullOrWhiteSpace(path)).ToArray()
            : [];
    }
}
