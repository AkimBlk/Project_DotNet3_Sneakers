using CommunityToolkit.Mvvm.ComponentModel;
using MyProjectBase.Models;

namespace MyProjectBase.ViewModels;

public partial class CollectionDetailsViewModel : ViewModelBase
{
    [ObservableProperty] private Shoe _myShoe;

    public CollectionDetailsViewModel()
    {
        MyShoe = new Shoe
        {
            Brand = "Design",
            Model = "Preview"
        };
    }

    public CollectionDetailsViewModel(Shoe shoe)
    {
        MyShoe = shoe;
    }
}
