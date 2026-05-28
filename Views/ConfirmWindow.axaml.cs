using Avalonia.Controls;
using Avalonia.Interactivity;

namespace MyProjectBase.Views;

public partial class ConfirmWindow : Window
{
    public ConfirmWindow() 
    {
        // Constructeur utilise par le designer Avalonia.
        InitializeComponent();
    }

    public ConfirmWindow(string message)
    {
        // Constructeur utilise par DialogService pour afficher une question precise.
        InitializeComponent();
        MessageText.Text = message;
    }
    
    
    private void BtnYes_Click(object? sender, RoutedEventArgs e)
    {
        // Ferme la fenetre modale en retournant true a DialogService.
        Close(true);
    }

    private void BtnNo_Click(object? sender, RoutedEventArgs e)
    {
        // Ferme la fenetre modale en retournant false a DialogService.
        Close(false);
    }
}
