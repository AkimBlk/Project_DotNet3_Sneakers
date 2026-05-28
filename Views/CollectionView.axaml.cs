using Avalonia.Controls;

namespace MyProjectBase.Views;

public partial class CollectionView:UserControl
{
    public CollectionView()
    {
        // Charge CollectionView.axaml ; les actions des boutons sont dans CollectionViewModel.
        InitializeComponent();
    }
}
