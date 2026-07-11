using Avalonia.Controls;

namespace MyProjectBase.Views;

public partial class ScannerView : UserControl
{
    public ScannerView()
    {
        // Charge ScannerView.axaml ; la logique de connexion reste dans ScannerViewModel et ScannerManager.
        InitializeComponent();
    }
}
