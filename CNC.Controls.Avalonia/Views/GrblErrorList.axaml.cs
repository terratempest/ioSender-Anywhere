using Avalonia.Controls;
using CNC.Core;
using CNC.Localization.Avalonia;

namespace CNC.Controls.Avalonia.Views;

public partial class GrblErrorList : UserControl
{
    public GrblErrorList()
    {
        InitializeComponent();
        DgrErrors.Columns[0].Header = Localize.T("CNC.Controls.Avalonia.grblerrorlist.hdr_errorCode", "Code");
        DgrErrors.Columns[1].Header = Localize.T("CNC.Controls.Avalonia.grblerrorlist.hdr_errorMessage", "Message");
        DgrErrors.ItemsSource = GrblErrors.List
            .Where(entry => entry.Key != 0)
            .OrderBy(entry => entry.Key)
            .Select(entry => new CodeMessage(entry.Key, entry.Value))
            .ToList();
    }
}
