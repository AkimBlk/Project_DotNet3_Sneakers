using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using MongoDB.Bson;
using MyProjectBase.Helpers;
using MyProjectBase.Models;

using System.Threading.Tasks;
using MyProjectBase.Services;

using Avalonia;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Controls;

using MyProjectBase.Views;

namespace MyProjectBase.ViewModels;

public partial class CollectionViewModel : ViewModelBase
{
    private readonly JSONServices _jsonServices;

    public IRelayCommand<string> FromParentCommand { get; set; }
    public ObservableCollection<Shoe> MyObservableShoes { get; }

    [ObservableProperty]
    private Shoe? _selectedShoe;

    //add
    [ObservableProperty]
    private bool _isAddPanelVisible;
    [ObservableProperty]
    private string _newBrand = string.Empty;
    [ObservableProperty]
    private string _newModel = string.Empty;
    [ObservableProperty]
    private string _newImagePath = string.Empty;
    
    //edit
    [ObservableProperty] 
    private bool _isEditPanelVisible;
    [ObservableProperty]
    private string _editBrand = string.Empty;
    [ObservableProperty]
    private string _editModel = string.Empty;
    [ObservableProperty]
    private string _editImagePath = string.Empty;
    
    [ObservableProperty]
    private string _message = string.Empty;

    public ObservableCollection<string> AvailableImages { get; } = new()
    {
        "avares://MyProjectBase/Assets/nike_air1.png",
        "avares://MyProjectBase/Assets/nike_p6000.png",
        
        "avares://MyProjectBase/Assets/adidas_superstar.png",
        
        "avares://MyProjectBase/Assets/NB_PP.png",
        "avares://MyProjectBase/Assets/NB_1906.png",
        
        "avares://MyProjectBase/Assets/asics.png",
    };

    public CollectionViewModel(IRelayCommand<string> fromParentCommand)
    {
        _jsonServices = new JSONServices();
        FromParentCommand = fromParentCommand;
        MyObservableShoes = [];
    }

    
    
    [RelayCommand]
    private async Task LoadJsonAsync()
    {
        var shoes = await _jsonServices.GetShoesAsync();

        MyGlobals.MyShoes.Clear();
        MyObservableShoes.Clear();
        Message = string.Empty;

        foreach (var shoe in shoes)
        {
            if (!string.IsNullOrWhiteSpace(shoe.ImagePath))
            {
                try
                {
                    shoe.Picture = ImageHelper.LoadFromResource(new Uri(shoe.ImagePath));
                }
                catch (Exception)
                {
                }
            }
            MyGlobals.MyShoes.Add(shoe);
            MyObservableShoes.Add(shoe);
        }
    }

    
    
    [RelayCommand]
    private void AddShoe()
    {
        IsEditPanelVisible = false;
        IsAddPanelVisible = true;
    }

    [RelayCommand]
    private async Task ConfirmAddAsync()
    {
        
        if (string.IsNullOrWhiteSpace(NewBrand) || string.IsNullOrWhiteSpace(NewModel) || string.IsNullOrWhiteSpace(NewImagePath))
        {
            Message = "| Fill all fields !";
            return;
        }
        
        bool isConfirmed = await ConfirmWindow.ShowAsync("Sure add this sneaker ?");
        if (!isConfirmed) {return;}
        
        var newShoe = new Shoe
        {
            Id = Guid.NewGuid().ToString(),//fera auto l'id
            Brand = NewBrand,
            Model = NewModel,
            ImagePath = NewImagePath
        };

        MyGlobals.MyShoes.Add(newShoe);

        await _jsonServices.SetShoesAsync(new List<Shoe>(MyGlobals.MyShoes));

        NewBrand = string.Empty;
        NewModel = string.Empty;
        NewImagePath = string.Empty;
        IsAddPanelVisible = false;
        
        Message = "| Add success, reload JSON !";
    }
    
    [RelayCommand]
    private void CancelAdd()
    {
        IsAddPanelVisible = false;
    }

    
    
    [RelayCommand]
    private async Task ExportCsvAsync()
    {
        if (Application.Current?.ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
        {
            var topLevel = TopLevel.GetTopLevel(desktop.MainWindow);
            if (topLevel != null)
            {
                var csvService = new CsvServices(topLevel);
                await csvService.SaveDataAsync(new List<Shoe>(MyGlobals.MyShoes));
            }
        }
    }
    
    [RelayCommand]
    private async Task ImportCsvAsync()
    {
        
        bool isConfirmed = await ConfirmWindow.ShowAsync("Sure import CSV ?");
        if (!isConfirmed) return;
        
        if (Application.Current?.ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
        {
            var topLevel = TopLevel.GetTopLevel(desktop.MainWindow);
            if (topLevel != null)
            {
                var csvService = new CsvServices(topLevel);

                var importedShoes = await csvService.LoadDataAsync();
                
                if (importedShoes.Count > 0)
                {
                    foreach (var shoe in importedShoes)
                    {
                        MyGlobals.MyShoes.Add(shoe);
                        //MyObservableShoes.Add(shoe);
                    }
                    await _jsonServices.SetShoesAsync(new List<Shoe>(MyGlobals.MyShoes));
                    
                    Message = "| Import CSV ok, reload JSON !";
                }
            }
        }
    }
    
    
    
    [RelayCommand]
    private void OpenEditPanel()
    {
        if (SelectedShoe == null)
        {
            Message = "| Select a sneaker to edit";
            return;
        }

        EditBrand = SelectedShoe.Brand;
        EditModel = SelectedShoe.Model;
        EditImagePath = SelectedShoe.ImagePath;
        
        IsEditPanelVisible = true;
        IsAddPanelVisible = false;
    }

    [RelayCommand]
    private void CancelEdit()
    {
        IsEditPanelVisible = false;
    }

    [RelayCommand]
    private async Task ConfirmEditAsync()
    {
        if (SelectedShoe == null){return;}
        
        bool isConfirmed = await ConfirmWindow.ShowAsync($"Sure edit : \"{SelectedShoe.Brand} : {SelectedShoe.Model}\" ?");
        if (!isConfirmed){return;}
        
        var shoeToUpdate = MyGlobals.MyShoes.FirstOrDefault(s => s.Id == SelectedShoe.Id);
        if (shoeToUpdate != null)
        {
            shoeToUpdate.Brand = EditBrand;
            shoeToUpdate.Model = EditModel;
            shoeToUpdate.ImagePath = EditImagePath;
        }
        
        await _jsonServices.SetShoesAsync(new List<Shoe>(MyGlobals.MyShoes));

        IsEditPanelVisible = false;
        Message = "| Edit success, reload JSON !";
    }
    
    
    
    [RelayCommand]
    private async Task DeleteSelectedShoeAsync()
    {
        if (SelectedShoe != null)
        {
            bool isConfirmed = await ConfirmWindow.ShowAsync("Sure delete this sneaker ?");
            if (!isConfirmed)
            {
                SelectedShoe = null;
                return;
            }
            
            var shoeToRemove = MyGlobals.MyShoes.FirstOrDefault(shoe => shoe.Id == SelectedShoe.Id);
            MyGlobals.MyShoes.Remove(shoeToRemove);
            
            await _jsonServices.SetShoesAsync(new List<Shoe>(MyGlobals.MyShoes));
            SelectedShoe = null;
            
            Message = "| Delete success, reload JSON !";
        } else {
            Message = "| Select a sneaker to delete";
            return;
        }
    }
}