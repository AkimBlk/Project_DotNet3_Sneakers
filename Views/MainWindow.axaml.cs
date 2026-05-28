using Avalonia.Controls;

namespace MyProjectBase.Views;

public partial class MainWindow : Window
{
    public MainWindow()
    {
        // Charge le XAML de MainWindow ; la logique reste dans MainWindowViewModel.
        InitializeComponent();
    }
}
