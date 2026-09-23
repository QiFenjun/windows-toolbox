using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using WindowsToolbox.Core.Interfaces;
using WindowsToolbox.Core.Models;
using WindowsToolbox.Modules.KeepAwake.Models;
using WindowsToolbox.Modules.KeepAwake.Services;
using WindowsToolbox.Modules.KeepAwake.ViewModels;
using WindowsToolbox.Modules.KeepAwake.Views;
using WindowsToolbox.Modules.LockInspector.Interop;
using WindowsToolbox.Modules.LockInspector.Services;
using WindowsToolbox.Modules.LockInspector.ViewModels;
using WindowsToolbox.Modules.LockInspector.Views;
using WindowsToolbox.Modules.Utilities.QR.Services;
using WindowsToolbox.Modules.Utilities.QR.ViewModels;
using WindowsToolbox.Modules.Utilities.QR.Views;
using WindowsToolbox.Modules.Utilities.Color.Services;
using WindowsToolbox.Modules.Utilities.Color.ViewModels;
using WindowsToolbox.Modules.Utilities.Color.Views;
using WindowsToolbox.Modules.Utilities.Services;
using WindowsToolbox.Modules.Utilities.ViewModels;
using WindowsToolbox.Modules.Utilities.Views;

internal static class UiSmoke
{
    internal static void Run()
    {
        Exception? error=null;
        Thread thread=new(()=>
        {
            try
            {
                WindowsToolbox.App.App app=new(); app.InitializeComponent();
                using KeepAwakeService awake=new(new NoExecutionRequest());
                using LockInspectorViewModel inspector=new(new LockScanService(new NoRestartManager()));
                KeepAwakeViewModel awakeVm=new(awake,new MemorySettings());
                FakeUtilitiesClipboard clipboard=new();
                using QrToolsViewModel qrVm=new(new QrCodeService(),clipboard,clipboard);
                using ColorToolsViewModel colorVm=new(clipboard,new NoScreenPicker());
                colorVm.HexText="#80FF4A6B";
                UtilitiesViewModel utilitiesVm=new(qrVm,colorVm);
                string output=Directory.CreateDirectory(Path.Combine("artifacts","ui-smoke")).FullName;
                foreach(ThemeMode theme in new[]{ThemeMode.Light,ThemeMode.Dark})
                {
                    ResourceDictionary colors=app.Resources.MergedDictionaries.First(d=>d.Source?.OriginalString.Contains("Colors.Light.xaml")==true || d.Source?.OriginalString.Contains("Colors.Dark.xaml")==true);
                    int index=app.Resources.MergedDictionaries.IndexOf(colors);
                    app.Resources.MergedDictionaries[index]=new ResourceDictionary{Source=new Uri($"/Windows工具箱;component/Themes/Colors.{theme}.xaml",UriKind.Relative)};
                    UserControl[] views=[
                        new LockInspectorView(){DataContext=inspector},new KeepAwakeView(){DataContext=awakeVm},
                        new WindowsToolbox.Modules.WindowTools.Views.WindowToolsView(),
                        new WindowsToolbox.Modules.QuickLaunch.Views.QuickLaunchView(),
                        new WindowsToolbox.Modules.FileTools.Views.FileToolsView(),
                        new WindowsToolbox.Modules.TextTools.Views.TextToolsView(),
                        new WindowsToolbox.Modules.ClipboardPlus.Views.ClipboardPlusView(),
                        new WindowsToolbox.Modules.NetworkTraffic.Views.NetworkTrafficView(),
                        new WindowsToolbox.Modules.InstalledApps.Views.InstalledAppsView(),
                        new WindowsToolbox.Modules.Shutdown.Views.ShutdownView(),
                        new UtilitiesView(){DataContext=utilitiesVm},new QrToolsView(){DataContext=qrVm},new ColorToolsView(){DataContext=colorVm}];
                    foreach(UserControl view in views)
                    {
                        view.Measure(new Size(1040,950)); view.Arrange(new Rect(0,0,1040,950)); view.UpdateLayout();
                        if(view is LockInspectorView or KeepAwakeView or ColorToolsView)
                        {
                            RenderTargetBitmap bitmap=new(1040,950,96,96,PixelFormats.Pbgra32); bitmap.Render(view);
                            PngBitmapEncoder encoder=new(); encoder.Frames.Add(BitmapFrame.Create(bitmap));
                            using FileStream stream=File.Create(Path.Combine(output,$"{view.GetType().Name}-{theme}.png")); encoder.Save(stream);
                        }
                    }
                    Console.WriteLine($"PASS: {theme} BAML/resource/layout load for all 11 module views plus QR Tools and Color Tools pages; new module renders saved. No user interaction or native activity.");
                }
                app.Shutdown();
            }
            catch(Exception ex){error=ex;}
        });
        thread.SetApartmentState(ApartmentState.STA); thread.Start(); thread.Join();
        if(error is not null) throw new InvalidOperationException("Offscreen UI smoke failed",error);
    }
    private sealed class NoExecutionRequest : IExecutionStatePlatform
    {
        public ExecutionStateResult Set(ExecutionState flags)=>throw new InvalidOperationException("UI smoke must never request real/fake wakefulness");
    }
    private sealed class MemorySettings : ISettingsService
    {
        public AppSettings Settings {get;}=new();
        public string SettingsFilePath=>"unused";
        public Task LoadAsync()=>Task.CompletedTask;
        public Task SaveAsync()=>Task.CompletedTask;
    }
    private sealed class FakeUtilitiesClipboard : IImageClipboardAdapter, IUtilitiesTextClipboardAdapter
    {
        public BitmapSource? GetImage()=>null;
        public void SetImage(BitmapSource image)=>throw new InvalidOperationException("UI smoke must not write the real clipboard");
        public void SetText(string text)=>throw new InvalidOperationException("UI smoke must not write the real clipboard");
    }
    private sealed class NoScreenPicker : IScreenColorPicker
    {
        public Task<Color?> PickAsync(Action<Color> preview,CancellationToken cancellationToken)=>
            throw new InvalidOperationException("UI smoke must not read the screen");
    }
    private sealed class NoRestartManager : IRestartManagerClient
    {
        public int StartSession(out uint session){session=0;throw new InvalidOperationException("UI smoke must not scan");}
        public int RegisterResources(uint session,string[] files)=>throw new NotSupportedException();
        public int GetList(uint session,out uint needed,ref uint count,RmProcessInfo[]? processes,out uint rebootReasons)=>throw new NotSupportedException();
        public int EndSession(uint session)=>throw new NotSupportedException();
    }
}
