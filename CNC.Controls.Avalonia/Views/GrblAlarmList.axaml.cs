using Avalonia.Controls;
using CNC.Core;
using CNC.Localization.Avalonia;

namespace CNC.Controls.Avalonia.Views;

public partial class GrblAlarmList : UserControl
{
    public GrblAlarmList()
    {
        InitializeComponent();
        DgrAlarms.Columns[0].Header = Localize.T("CNC.Controls.Avalonia.grblalarmlist.hdr_alarmCode", "Code");
        DgrAlarms.Columns[1].Header = Localize.T("CNC.Controls.Avalonia.grblalarmlist.hdr_alarmMessage", "Message");
        DgrAlarms.ItemsSource = GrblAlarms.List
            .OrderBy(entry => entry.Key)
            .Select(entry => new CodeMessage(entry.Key, entry.Value))
            .ToList();
    }
}
