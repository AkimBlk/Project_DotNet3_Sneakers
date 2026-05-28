using Avalonia.Controls;

namespace MyProjectBase.Views;

public partial class CollectionDetailsView:UserControl
{
    public CollectionDetailsView()
    {
        // Charge la vue details ; elle affiche la sneaker fournie par CollectionDetailsViewModel.
        InitializeComponent();
    }
}
