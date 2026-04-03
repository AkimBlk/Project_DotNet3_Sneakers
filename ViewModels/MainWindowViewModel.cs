
using System;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using MongoDB.Bson;
using MyProjectBase.Helpers;
using MyProjectBase.Models;
    
namespace MyProjectBase.ViewModels;

public partial class MainWindowViewModel : ViewModelBase
{
    [ObservableProperty] private ViewModelBase _currentPage;

    public MainWindowViewModel()
    {
        CurrentPage = new CollectionViewModel(GoToDetailsFromChildCommand);
    }
    partial void OnCurrentPageChanging(ViewModelBase? oldValue, ViewModelBase? newValue)
    {
        oldValue?.Dispose();
    }
    
    [RelayCommand]
    private void GoToDetailsFromChild(string shoeId)
    {
        CurrentPage = new CollectionDetailsViewModel(shoeId);
    }
    
    [RelayCommand]
    private void BackToMain()
    {
        CurrentPage = new CollectionViewModel(GoToDetailsFromChildCommand);
    }
}

