using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using MyProjectBase.Helpers;
using MyProjectBase.Models;
using MyProjectBase.Repositories;
using MyProjectBase.Services;
using MyProjectBase.Utilities;

namespace MyProjectBase.ViewModels;

public partial class CollectionViewModel : ViewModelBase
{
    private readonly IShoeRepository _shoeRepository;
    private readonly IJsonShoeService _jsonShoeService;
    private readonly ICsvService _csvService;
    private readonly IDialogService _dialogService;
    private readonly IAppLogger _logger;

    public IRelayCommand<string> FromParentCommand { get; }
    public ObservableCollection<Shoe> MyObservableShoes => _shoeRepository.Shoes;

    [ObservableProperty] private Shoe? _selectedShoe;
    [ObservableProperty] private bool _isAddPanelVisible;
    [ObservableProperty] private string _newBrand = string.Empty;
    [ObservableProperty] private string _newModel = string.Empty;
    [ObservableProperty] private string _newImagePath = string.Empty;
    [ObservableProperty] private bool _isEditPanelVisible;
    [ObservableProperty] private string _editBrand = string.Empty;
    [ObservableProperty] private string _editModel = string.Empty;
    [ObservableProperty] private string _editImagePath = string.Empty;
    [ObservableProperty] private string _message = "Load remote JSON or import a CSV file.";
    [ObservableProperty] private bool _isBusy;

    public ObservableCollection<string> AvailableImages { get; } =
    [
        "avares://MyProjectBase/Assets/nike_air1.png",
        "avares://MyProjectBase/Assets/nike_p6000.png",
        "avares://MyProjectBase/Assets/adidas_superstar.png",
        "avares://MyProjectBase/Assets/NB_PP.png",
        "avares://MyProjectBase/Assets/NB_1906.png",
        "avares://MyProjectBase/Assets/asics.png"
    ];

    public CollectionViewModel()
        : this(
            new RelayCommand<string>(_ => { }),
            new ShoeRepository(),
            new JsonShoeService(new AppLogger()),
            new CsvService(new AppLogger()),
            new DialogService(),
            new AppLogger())
    {
    }

    public CollectionViewModel(
        IRelayCommand<string> fromParentCommand,
        IShoeRepository shoeRepository,
        IJsonShoeService jsonShoeService,
        ICsvService csvService,
        IDialogService dialogService,
        IAppLogger logger)
    {
        FromParentCommand = fromParentCommand;
        _shoeRepository = shoeRepository;
        _jsonShoeService = jsonShoeService;
        _csvService = csvService;
        _dialogService = dialogService;
        _logger = logger;
    }

    [RelayCommand]
    private async Task LoadJsonAsync()
    {
        IsBusy = true;
        Message = "Loading JSON...";

        try
        {
            var result = await _jsonShoeService.GetShoesAsync(CancellationToken);
            if (!result.Success || result.Value == null)
            {
                Message = result.Message;
                return;
            }

            foreach (var shoe in result.Value)
                shoe.Picture = ImageHelper.LoadShoePicture(shoe);

            _shoeRepository.ReplaceAll(result.Value);
            SelectedShoe = null;
            Message = result.Message;
        }
        finally
        {
            IsBusy = false;
        }
    }

    [RelayCommand]
    private void AddShoe()
    {
        IsEditPanelVisible = false;
        IsAddPanelVisible = true;
        Message = "Creating a sneaker.";
    }

    [RelayCommand]
    private async Task ConfirmAddAsync()
    {
        var newShoe = new Shoe
        {
            Id = Guid.NewGuid().ToString(),
            Brand = NewBrand.Trim(),
            Model = NewModel.Trim(),
            ImagePath = NewImagePath.Trim()
        };

        var errors = ShoeValidator.Validate(newShoe);
        if (errors.Count > 0)
        {
            Message = string.Join(" ", errors);
            return;
        }

        var isConfirmed = await _dialogService.ConfirmAsync("Add this sneaker?");
        if (!isConfirmed)
        {
            Message = "Add canceled.";
            return;
        }

        newShoe.Picture = ImageHelper.LoadShoePicture(newShoe);
        _shoeRepository.Add(newShoe);

        var result = await _jsonShoeService.SetShoesAsync(_shoeRepository.Shoes, CancellationToken);
        if (!result.Success)
        {
            Message = result.Message;
            return;
        }

        NewBrand = string.Empty;
        NewModel = string.Empty;
        NewImagePath = string.Empty;
        IsAddPanelVisible = false;
        Message = "Sneaker added.";
    }

    [RelayCommand]
    private void CancelAdd()
    {
        IsAddPanelVisible = false;
        Message = "Add canceled.";
    }

    [RelayCommand]
    private async Task ExportCsvAsync()
    {
        IsBusy = true;
        try
        {
            var result = await _csvService.SaveDataAsync(_shoeRepository.Shoes, CancellationToken);
            Message = result.Message;
        }
        finally
        {
            IsBusy = false;
        }
    }

    [RelayCommand]
    private async Task ImportCsvAsync()
    {
        var isConfirmed = await _dialogService.ConfirmAsync("Import this CSV and replace the current collection?");
        if (!isConfirmed)
        {
            Message = "CSV import canceled.";
            return;
        }

        IsBusy = true;
        try
        {
            var imported = await _csvService.LoadDataAsync(CancellationToken);
            if (!imported.Success || imported.Value == null)
            {
                Message = imported.Message;
                return;
            }

            _shoeRepository.ReplaceAll(imported.Value);

            var saveResult = await _jsonShoeService.SetShoesAsync(_shoeRepository.Shoes, CancellationToken);
            Message = saveResult.Success ? imported.Message : saveResult.Message;
        }
        finally
        {
            IsBusy = false;
        }
    }

    [RelayCommand]
    private void OpenEditPanel()
    {
        if (SelectedShoe == null)
        {
            Message = "Select a sneaker to edit.";
            return;
        }

        EditBrand = SelectedShoe.Brand;
        EditModel = SelectedShoe.Model;
        EditImagePath = SelectedShoe.ImagePath;

        IsEditPanelVisible = true;
        IsAddPanelVisible = false;
        Message = $"Modification de {SelectedShoe.Brand} {SelectedShoe.Model}.";
    }

    [RelayCommand]
    private void CancelEdit()
    {
        IsEditPanelVisible = false;
        Message = "Edit canceled.";
    }

    [RelayCommand]
    private async Task ConfirmEditAsync()
    {
        if (SelectedShoe == null)
            return;

        var updated = new Shoe
        {
            Id = SelectedShoe.Id,
            Brand = EditBrand.Trim(),
            Model = EditModel.Trim(),
            ImagePath = EditImagePath.Trim()
        };

        var errors = ShoeValidator.Validate(updated);
        if (errors.Count > 0)
        {
            Message = string.Join(" ", errors);
            return;
        }

        var isConfirmed = await _dialogService.ConfirmAsync($"Edit {SelectedShoe.Brand} {SelectedShoe.Model}?");
        if (!isConfirmed)
        {
            Message = "Edit canceled.";
            return;
        }

        SelectedShoe.Brand = updated.Brand;
        SelectedShoe.Model = updated.Model;
        SelectedShoe.ImagePath = updated.ImagePath;
        SelectedShoe.Picture = ImageHelper.LoadShoePicture(SelectedShoe);

        var result = await _jsonShoeService.SetShoesAsync(_shoeRepository.Shoes, CancellationToken);
        IsEditPanelVisible = false;
        Message = result.Success ? "Sneaker updated." : result.Message;
    }

    [RelayCommand]
    private async Task DeleteSelectedShoeAsync()
    {
        if (SelectedShoe == null)
        {
            Message = "Select a sneaker to delete.";
            return;
        }

        var deletedInfo = $"{SelectedShoe.Brand} {SelectedShoe.Model}";
        var isConfirmed = await _dialogService.ConfirmAsync($"Delete {deletedInfo}?");
        if (!isConfirmed)
        {
            Message = "Delete canceled.";
            return;
        }

        _shoeRepository.Remove(SelectedShoe.Id);
        SelectedShoe = null;

        var result = await _jsonShoeService.SetShoesAsync(_shoeRepository.Shoes, CancellationToken);
        Message = result.Success ? $"Deleted: {deletedInfo}." : result.Message;
    }

}
