using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using MyProjectBase.Models;

namespace MyProjectBase.ViewModels;

public partial class CollectionDetailsViewModel : ViewModelBase
{
    [ObservableProperty] private Shoe _myShoe;
    public IRelayCommand BackCommand { get; }

    public CollectionDetailsViewModel()
    {
        BackCommand = new RelayCommand(() => { });
        MyShoe = new Shoe
        {
            Brand = "Design",
            Model = "Preview"
        };
    }

    public CollectionDetailsViewModel(Shoe shoe, IRelayCommand backCommand)
    {
        MyShoe = shoe;
        BackCommand = backCommand;
    }
}
