using System.Threading.Tasks;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;
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
    
    
    public static async Task<bool> ShowAsync(string message)
    {
        if (Application.Current?.ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop && desktop.MainWindow != null)
        {
            var window = new ConfirmWindow(message);
            return await window.ShowDialog<bool>(desktop.MainWindow); 
        }
        return false;
    }
}