using Avalonia.Controls;

namespace MyProjectBase.Views;

public partial class AdminUsersView : UserControl
{
    public AdminUsersView()
    {
        // Charge AdminUsersView.axaml ; la gestion MongoDB passe par AdminUsersViewModel.
        InitializeComponent();
    }
}
