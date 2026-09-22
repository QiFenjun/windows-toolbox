using System.Windows;
using System.Windows.Controls;
using WindowsToolbox.Modules.LockInspector.Views;
using WindowsToolbox.Modules.LockInspector.ViewModels;
using WindowsToolbox.Modules.LockInspector.Services;
using WindowsToolbox.Modules.KeepAwake.Services;
using WindowsToolbox.Modules.KeepAwake.ViewModels;
using WindowsToolbox.Modules.KeepAwake.Views;

namespace WindowsToolbox.Tests;

[TestClass]
public sealed class ToolViewThemeTests
{
    [DataTestMethod][DataRow("Light",false)][DataRow("Dark",false)][DataRow("Light",true)][DataRow("Dark",true)]
    public void ToolViewLoadsAndLaysOutWithSharedTheme(string theme,bool awake)
    {
        Exception? error=null;
        Thread thread=new(()=>
        {
            try
            {
                using LockInspectorViewModel vm=new(new LockScanService(new FakeRestartManager()));
                using KeepAwakeService service=new(new FakeExecutionState());
                Verify(awake ? new KeepAwakeView(){DataContext=new KeepAwakeViewModel(service,new AwakeSettings())}
                    : new LockInspectorView(){DataContext=vm},theme);
            }
            catch(Exception ex) {error=ex;}
        });
        thread.SetApartmentState(ApartmentState.STA); thread.Start();
        Assert.IsTrue(thread.Join(TimeSpan.FromSeconds(10)),"Theme loading timed out.");
        if(error is not null) throw new AssertFailedException(error.ToString());
    }

    internal static void Verify(UserControl view,string theme)
    {
        foreach(string name in new[]{"Colors",$"Colors.{theme}","Motion","Typography","CardStyles","ButtonStyles","ControlStyles","ScrollBarStyles"})
            view.Resources.MergedDictionaries.Add(new ResourceDictionary{Source=new Uri($"/Windows工具箱;component/Themes/{name}.xaml",UriKind.Relative)});
        Assert.IsNotNull(view.TryFindResource("CardStyle")); Assert.IsNotNull(view.TryFindResource("PrimaryTextBrush"));
        view.Measure(new Size(1000,900)); view.Arrange(new Rect(0,0,1000,900)); view.UpdateLayout();
        Assert.IsTrue(view.ActualWidth>0);
    }
}
