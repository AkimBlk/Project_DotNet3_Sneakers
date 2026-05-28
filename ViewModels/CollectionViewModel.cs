using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.ComponentModel;
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
    // ViewModel principal de la collection : il contient les commandes CRUD, CSV et JSON.
    private readonly IShoeRepository _shoeRepository;
    private readonly IJsonShoeService _jsonShoeService;
    private readonly ICsvService _csvService;
    private readonly IDialogService _dialogService;
    private readonly IAppLogger _logger;
    private readonly Func<string> _collectionKeyProvider;

    public IRelayCommand<string> FromParentCommand { get; }
    public ObservableCollection<Shoe> MyObservableShoes => _shoeRepository.Shoes;

    // Donnees calculees pour afficher un petit resume du stock par groupe dans l'interface.
    public ObservableCollection<GroupStockSummary> StockByGroup { get; } = [];

    [ObservableProperty] private Shoe? _selectedShoe;
    [ObservableProperty] private bool _isAddPanelVisible;
    [ObservableProperty] private string _newId = string.Empty;
    [ObservableProperty] private string _newBrand = string.Empty;
    [ObservableProperty] private string _newModel = string.Empty;
    [ObservableProperty] private string _newGroup = "Sneakers";
    [ObservableProperty] private int _newStock;
    [ObservableProperty] private decimal _newPrice;
    [ObservableProperty] private string _newImagePath = string.Empty;
    [ObservableProperty] private bool _isEditPanelVisible;
    [ObservableProperty] private string _editBrand = string.Empty;
    [ObservableProperty] private string _editModel = string.Empty;
    [ObservableProperty] private string _editGroup = "Sneakers";
    [ObservableProperty] private int _editStock;
    [ObservableProperty] private decimal _editPrice;
    [ObservableProperty] private string _editImagePath = string.Empty;
    [ObservableProperty] private string _message = "Load remote JSON or import a CSV file.";
    [ObservableProperty] private bool _isBusy;
    [ObservableProperty] private bool _exportId = true;
    [ObservableProperty] private bool _exportBrand = true;
    [ObservableProperty] private bool _exportModel = true;
    [ObservableProperty] private bool _exportGroup = true;
    [ObservableProperty] private bool _exportStock = true;
    [ObservableProperty] private bool _exportPrice = true;
    [ObservableProperty] private bool _exportImagePath = true;

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
            new AppLogger(),
            () => "design")
    {
    }

    public CollectionViewModel(
        IRelayCommand<string> fromParentCommand,
        IShoeRepository shoeRepository,
        IJsonShoeService jsonShoeService,
        ICsvService csvService,
        IDialogService dialogService,
        IAppLogger logger,
        Func<string> collectionKeyProvider)
    {
        // Le repository expose une ObservableCollection : la liste UI se met a jour par data binding.
        FromParentCommand = fromParentCommand;
        _shoeRepository = shoeRepository;
        _jsonShoeService = jsonShoeService;
        _csvService = csvService;
        _dialogService = dialogService;
        _logger = logger;
        _collectionKeyProvider = collectionKeyProvider;
        // Ces abonnements gardent le graphique de stock synchronise avec les ajouts/modifications.
        _shoeRepository.Shoes.CollectionChanged += ShoesCollectionChanged;
        foreach (var shoe in _shoeRepository.Shoes)
            shoe.PropertyChanged += ShoePropertyChanged;
        RefreshStockChart();
    }

    [RelayCommand]
    private async Task LoadJsonAsync()
    {
        // Charge depuis le serveur distant la collection associee a l'utilisateur actif.
        IsBusy = true;
        Message = "Loading JSON...";

        try
        {
            var result = await _jsonShoeService.GetShoesAsync(_collectionKeyProvider(), CancellationToken);
            if (!result.Success || result.Value == null)
            {
                // On initialise seulement quand le fichier n'existe pas ; une erreur reseau ne doit pas ecraser les donnees.
                if (result.Kind == ServiceResultKind.NotFound)
                    await InitializeDefaultRemoteCollectionAsync("No remote collection found; default collection is ready.");
                else
                    Message = result.Message;
                return;
            }

            if (result.Value.Count == 0)
            {
                await InitializeDefaultRemoteCollectionAsync("Remote collection was empty; default collection created.");

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

    public void UseScannedId(string scannedId)
    {
        // Methode appelee par MainWindowViewModel quand le scanner lit un ID pendant un ajout.
        if (string.IsNullOrWhiteSpace(scannedId))
            return;

        NewId = scannedId.Trim();
        IsAddPanelVisible = true;
        IsEditPanelVisible = false;
        Message = "Identifier filled from scanner.";
    }

    [RelayCommand]
    private async Task ConfirmAddAsync()
    {
        // Construit une nouvelle sneaker a partir des champs du formulaire d'ajout.
        var newShoe = new Shoe
        {
            Id = string.IsNullOrWhiteSpace(NewId) ? Guid.NewGuid().ToString() : NewId.Trim(),
            Brand = NewBrand.Trim(),
            Model = NewModel.Trim(),
            Group = string.IsNullOrWhiteSpace(NewGroup) ? "Sneakers" : NewGroup.Trim(),
            Stock = NewStock,
            Price = NewPrice,
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

        // Apres ajout local, on sauvegarde immediatement la collection distante.
        var result = await _jsonShoeService.SetShoesAsync(_collectionKeyProvider(), _shoeRepository.Shoes, CancellationToken);
        if (!result.Success)
        {
            Message = result.Message;
            return;
        }

        NewBrand = string.Empty;
        NewId = string.Empty;
        NewModel = string.Empty;
        NewGroup = "Sneakers";
        NewStock = 0;
        NewPrice = 0;
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

    private async Task InitializeDefaultRemoteCollectionAsync(string successMessage)
    {
        // Collection de depart utilisee quand un compte n'a pas encore de fichier JSON distant.
        var defaults = DefaultShoeCatalog.Create();
        foreach (var shoe in defaults)
            shoe.Picture = ImageHelper.LoadShoePicture(shoe);

        _shoeRepository.ReplaceAll(defaults);
        SelectedShoe = null;

        var saveResult = await _jsonShoeService.SetShoesAsync(_collectionKeyProvider(), _shoeRepository.Shoes, CancellationToken);
        Message = saveResult.Success ? successMessage : $"{successMessage} Remote save unavailable.";
    }

    [RelayCommand]
    private async Task ExportCsvAsync()
    {
        // Les booleens Export* correspondent aux cases cochees dans l'interface.
        IsBusy = true;
        try
        {
            var options = new CsvExportOptions(ExportId, ExportBrand, ExportModel, ExportGroup, ExportStock, ExportPrice, ExportImagePath);
            var result = await _csvService.SaveDataAsync(_shoeRepository.Shoes, options, CancellationToken);
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
        // L'import ajoute au lieu de remplacer pour respecter le cahier des charges.
        var isConfirmed = await _dialogService.ConfirmAsync("Import this CSV and add its sneakers to the current collection?");
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

            var added = 0;
            foreach (var shoe in imported.Value)
            {
                // Le repository refuse deja les doublons d'ID ; on compte seulement les vrais ajouts.
                var before = _shoeRepository.Shoes.Count;
                _shoeRepository.Add(shoe);
                if (_shoeRepository.Shoes.Count > before)
                    added++;
            }

            var saveResult = await _jsonShoeService.SetShoesAsync(_collectionKeyProvider(), _shoeRepository.Shoes, CancellationToken);
            Message = saveResult.Success ? $"{added} sneakers added from CSV." : saveResult.Message;
        }
        finally
        {
            IsBusy = false;
        }
    }

    [RelayCommand]
    private void OpenEditPanel()
    {
        // Copie les valeurs selectionnees dans le formulaire d'edition.
        if (SelectedShoe == null)
        {
            Message = "Select a sneaker to edit.";
            return;
        }

        EditBrand = SelectedShoe.Brand;
        EditModel = SelectedShoe.Model;
        EditGroup = SelectedShoe.Group;
        EditStock = SelectedShoe.Stock;
        EditPrice = SelectedShoe.Price;
        EditImagePath = SelectedShoe.ImagePath;

        IsEditPanelVisible = true;
        IsAddPanelVisible = false;
        Message = $"Editing {SelectedShoe.Brand} {SelectedShoe.Model}.";
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
        // L'ID reste stable pendant une edition : il identifie l'objet collectionne.
        if (SelectedShoe == null)
            return;

        var updated = new Shoe
        {
            Id = SelectedShoe.Id,
            Brand = EditBrand.Trim(),
            Model = EditModel.Trim(),
            Group = string.IsNullOrWhiteSpace(EditGroup) ? "Sneakers" : EditGroup.Trim(),
            Stock = EditStock,
            Price = EditPrice,
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
        SelectedShoe.Group = updated.Group;
        SelectedShoe.Stock = updated.Stock;
        SelectedShoe.Price = updated.Price;
        SelectedShoe.ImagePath = updated.ImagePath;
        SelectedShoe.Picture = ImageHelper.LoadShoePicture(SelectedShoe);

        // Sauvegarde distante apres modification.
        var result = await _jsonShoeService.SetShoesAsync(_collectionKeyProvider(), _shoeRepository.Shoes, CancellationToken);
        IsEditPanelVisible = false;
        Message = result.Success ? "Sneaker updated." : result.Message;
    }

    [RelayCommand]
    private async Task DeleteSelectedShoeAsync()
    {
        // Suppression avec confirmation pour eviter une perte accidentelle.
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

        var result = await _jsonShoeService.SetShoesAsync(_collectionKeyProvider(), _shoeRepository.Shoes, CancellationToken);
        Message = result.Success ? $"Deleted: {deletedInfo}." : result.Message;
    }

    private void ShoesCollectionChanged(object? sender, NotifyCollectionChangedEventArgs e)
    {
        // Quand la collection change, on abonne/desabonne les PropertyChanged des sneakers concernees.
        if (e.OldItems != null)
        {
            foreach (Shoe shoe in e.OldItems)
                shoe.PropertyChanged -= ShoePropertyChanged;
        }

        if (e.NewItems != null)
        {
            foreach (Shoe shoe in e.NewItems)
                shoe.PropertyChanged += ShoePropertyChanged;
        }

        RefreshStockChart();
    }

    private void ShoePropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        // Le graphique doit etre recalcule uniquement si le stock ou le groupe change.
        if (e.PropertyName is nameof(Shoe.Group) or nameof(Shoe.Stock))
            RefreshStockChart();
    }

    private void RefreshStockChart()
    {
        // Calcule une largeur de barre proportionnelle au stock maximum.
        var groups = _shoeRepository.Shoes
            .GroupBy(shoe => string.IsNullOrWhiteSpace(shoe.Group) ? "Other" : shoe.Group)
            .Select(group => new { Name = group.Key, Stock = group.Sum(shoe => shoe.Stock) })
            .OrderBy(group => group.Name)
            .ToList();

        var max = Math.Max(1, groups.Count == 0 ? 1 : groups.Max(group => group.Stock));
        StockByGroup.Clear();

        foreach (var group in groups)
            StockByGroup.Add(new GroupStockSummary(group.Name, group.Stock, 40 + (double)group.Stock / max * 220));
    }

    protected override void Dispose(bool disposing)
    {
        if (!disposing)
            return;

        _shoeRepository.Shoes.CollectionChanged -= ShoesCollectionChanged;
        foreach (var shoe in _shoeRepository.Shoes)
            shoe.PropertyChanged -= ShoePropertyChanged;
    }
}

public sealed record GroupStockSummary(string Group, int Stock, double BarWidth);
