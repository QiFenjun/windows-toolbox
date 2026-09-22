using System.Windows.Controls;
using System.Windows.Threading;
using WindowsToolbox.Modules.KeepAwake.ViewModels;

namespace WindowsToolbox.Modules.KeepAwake.Views;

public partial class KeepAwakeView : UserControl
{
    private readonly DispatcherTimer _displayTimer=new(){Interval=TimeSpan.FromSeconds(1)};
    public KeepAwakeView()
    {
        InitializeComponent();
        _displayTimer.Tick+=(_,_)=>Refresh();
        Loaded+=(_,_)=>{Refresh();_displayTimer.Start();};
        Unloaded+=(_,_)=>_displayTimer.Stop(); // Display only. The module-owned worker continues in tray/navigation.
    }
    private void Refresh() { if(DataContext is KeepAwakeViewModel model) model.Refresh(); }
}
