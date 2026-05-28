using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using MyProjectBase.Models;

namespace MyProjectBase.ViewModels;

public partial class CollectionDetailsViewModel : ViewModelBase
{
    // Sneaker affichee par CollectionDetailsView.
    [ObservableProperty] private Shoe _myShoe;

    // Commande retour fournie par MainWindowViewModel pour revenir a la collection.
    public IRelayCommand BackCommand { get; }

    public CollectionDetailsViewModel()
    {
        // Constructeur utilise seulement par le designer Avalonia.
        BackCommand = new RelayCommand(() => { });
        MyShoe = new Shoe
        {
            Brand = "Design",
            Model = "Preview"
        };
    }

    public CollectionDetailsViewModel(Shoe shoe, IRelayCommand backCommand)
    {
        // Recoit l'objet selectionne dans CollectionView et la commande de retour.
        MyShoe = shoe;
        BackCommand = backCommand;
    }
}
