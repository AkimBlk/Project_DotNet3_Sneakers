using System;
using System.Linq;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using MongoDB.Bson;
using MyProjectBase.Helpers;
using MyProjectBase.Models;

namespace MyProjectBase.ViewModels;

public partial class CollectionDetailsViewModel : ViewModelBase
{
    [ObservableProperty] private Shoe _myShoe;
    
    public CollectionDetailsViewModel(string id)
    {
        MyShoe = MyGlobals.MyShoes.First(shoe => shoe.Id == id);
    }
}