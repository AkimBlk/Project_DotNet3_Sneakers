using Avalonia.Controls;
using Avalonia.Interactivity;

namespace MyProjectBase.Views;

public partial class ConfirmWindow : Window
{
    public ConfirmWindow() 
    {
        InitializeComponent();
    }
    public ConfirmWindow(string message)
    {
        InitializeComponent();
        MessageText.Text = message;
    }
    
    
    private void BtnYes_Click(object? sender, RoutedEventArgs e)
    {
        Close(true);
    }
    private void BtnNo_Click(object? sender, RoutedEventArgs e)
    {
        Close(false);
    }
}
