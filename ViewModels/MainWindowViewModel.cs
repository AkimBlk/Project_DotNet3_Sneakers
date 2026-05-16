using System.Text.Json;
using Avalonia.Threading;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using MyProjectBase.Helpers;
using MyProjectBase.Models;
using MyProjectBase.Repositories;
using MyProjectBase.Services;
using MyProjectBase.Utilities;

namespace MyProjectBase.ViewModels;

public partial class MainWindowViewModel : ViewModelBase
{
    private readonly IShoeRepository _shoeRepository;
    private readonly IUserRepository _userRepository;
    private readonly IJsonShoeService _jsonShoeService;
    private readonly ICsvService _csvService;
    private readonly IDialogService _dialogService;
    private readonly IScannerManager _scannerManager;
    private readonly IAppLogger _logger;

    [ObservableProperty] private ViewModelBase _currentPage;
    [ObservableProperty] private string _qrCode = "No scan";
    [ObservableProperty] private string _statusMessage = "Ready.";
    [ObservableProperty] private bool _isScannerConnected;
    [ObservableProperty] private bool _isBusy;

    [ObservableProperty] private string _loginEmail = string.Empty;
    [ObservableProperty] private string _loginPassword = string.Empty;
    [ObservableProperty] private string _registerDisplayName = string.Empty;
    [ObservableProperty] private string _registerEmail = string.Empty;
    [ObservableProperty] private string _registerPassword = string.Empty;
    [ObservableProperty] private UserAccount? _currentUser;

    public MainWindowViewModel()
        : this(
            new ShoeRepository(),
            new MongoUserRepository(new AppLogger()),
            new JsonShoeService(new AppLogger()),
            new CsvService(new AppLogger()),
            new DialogService(),
            new ScannerManager(new AppLogger()),
            new AppLogger())
    {
    }

    public MainWindowViewModel(
        IShoeRepository shoeRepository,
        IUserRepository userRepository,
        IJsonShoeService jsonShoeService,
        ICsvService csvService,
        IDialogService dialogService,
        IScannerManager scannerManager,
        IAppLogger logger)
    {
        _shoeRepository = shoeRepository;
        _userRepository = userRepository;
        _jsonShoeService = jsonShoeService;
        _csvService = csvService;
        _dialogService = dialogService;
        _scannerManager = scannerManager;
        _logger = logger;

        _scannerManager.CodeReceived += ScannerCodeReceived;
        SeedDefaultCollection("Default collection loaded.");
        _currentPage = CreateCollectionViewModel();
    }

    public bool IsAuthenticated => CurrentUser != null;
    public bool IsAdmin => CurrentUser?.Role == UserRole.Admin;
    public string ActiveCollectionKey => CurrentUser?.CollectionKey ?? "guest";

    partial void OnCurrentUserChanged(UserAccount? value)
    {
        OnPropertyChanged(nameof(IsAuthenticated));
        OnPropertyChanged(nameof(IsAdmin));
    }

    partial void OnCurrentPageChanging(ViewModelBase? oldValue, ViewModelBase newValue)
    {
        oldValue?.Dispose();
    }

    [RelayCommand]
    private async Task LoginAsync()
    {
        IsBusy = true;
        try
        {
            var result = await _userRepository.LoginAsync(LoginEmail, LoginPassword);
            ApplyUserResult(result);
        }
        finally
        {
            IsBusy = false;
            LoginPassword = string.Empty;
        }
    }

    [RelayCommand]
    private async Task RegisterAsync()
    {
        IsBusy = true;
        try
        {
            var result = await _userRepository.RegisterAsync(RegisterEmail, RegisterDisplayName, RegisterPassword);
            ApplyUserResult(result);
        }
        finally
        {
            IsBusy = false;
            RegisterPassword = string.Empty;
        }
    }

    [RelayCommand]
    private void Logout()
    {
        CurrentUser = null;
        SeedDefaultCollection("Logged out. Default collection loaded.");
        CurrentPage = CreateCollectionViewModel();
    }

    [RelayCommand]
    private void GoToAdmin()
    {
        if (!IsAdmin)
        {
            StatusMessage = "Admin access required.";
            return;
        }

        CurrentPage = new AdminUsersViewModel(_userRepository, _dialogService, _logger, CurrentUser!.Id);
        StatusMessage = "Administration displayed.";
    }

    [RelayCommand]
    private void GoToDetailsFromChild(string shoeId)
    {
        var shoe = _shoeRepository.FindById(shoeId);
        if (shoe == null)
        {
            StatusMessage = "Sneaker not found.";
            return;
        }

        CurrentPage = new CollectionDetailsViewModel(shoe, BackToMainCommand);
    }

    [RelayCommand]
    private void BackToMain()
    {
        CurrentPage = CreateCollectionViewModel();
        StatusMessage = "Collection displayed.";
    }

    [RelayCommand]
    private void ConnectScanner()
    {
        var result = _scannerManager.OpenPort();
        IsScannerConnected = _scannerManager.IsConnected;
        StatusMessage = result.Message;
    }

    [RelayCommand]
    private void DisconnectScanner()
    {
        var result = _scannerManager.ClosePort();
        IsScannerConnected = _scannerManager.IsConnected;
        StatusMessage = result.Message;
    }

    private CollectionViewModel CreateCollectionViewModel()
    {
        return new CollectionViewModel(
            GoToDetailsFromChildCommand,
            _shoeRepository,
            _jsonShoeService,
            _csvService,
            _dialogService,
            _logger,
            () => ActiveCollectionKey);
    }

    private void SeedDefaultCollection(string message)
    {
        var shoes = DefaultShoeCatalog.Create();
        foreach (var shoe in shoes)
            shoe.Picture = ImageHelper.LoadShoePicture(shoe);

        _shoeRepository.ReplaceAll(shoes);
        StatusMessage = message;
    }

    private void ApplyUserResult(ServiceResult<UserAccount> result)
    {
        if (result.Success && result.Value != null)
        {
            CurrentUser = result.Value;
            CurrentPage = CreateCollectionViewModel();
            _ = LoadActiveCollectionAsync();
            LoginEmail = string.Empty;
            RegisterEmail = string.Empty;
            RegisterDisplayName = string.Empty;
        }

        StatusMessage = result.Message;
    }

    private void ScannerCodeReceived(object? sender, string raw)
    {
        Dispatcher.UIThread.Post(async () => await HandleScanAsync(raw));
    }

    private async Task HandleScanAsync(string raw)
    {
        if (string.IsNullOrWhiteSpace(raw))
            return;

        QrCode = raw.Length > 80 ? $"{raw[..80]}..." : raw;

        var existingByRawId = _shoeRepository.FindById(raw);
        if (existingByRawId != null)
        {
            CurrentPage = new CollectionDetailsViewModel(existingByRawId, BackToMainCommand);
            StatusMessage = "Sneaker found from scan.";
            return;
        }

        try
        {
            var shoe = JsonSerializer.Deserialize<Shoe>(
                raw,
                new JsonSerializerOptions { PropertyNameCaseInsensitive = true });

            if (shoe == null)
            {
                StatusMessage = "QR code not recognized.";
                return;
            }

            if (string.IsNullOrWhiteSpace(shoe.Id))
                shoe.Id = Guid.NewGuid().ToString();

            var errors = ShoeValidator.Validate(shoe);
            if (errors.Count > 0)
            {
                StatusMessage = string.Join(" ", errors);
                return;
            }

            var duplicate = _shoeRepository.FindById(shoe.Id) ??
                            _shoeRepository.Shoes.FirstOrDefault(existing =>
                                existing.Brand.Equals(shoe.Brand, StringComparison.OrdinalIgnoreCase) &&
                                existing.Model.Equals(shoe.Model, StringComparison.OrdinalIgnoreCase));

            if (duplicate != null)
            {
                CurrentPage = new CollectionDetailsViewModel(duplicate, BackToMainCommand);
                StatusMessage = "Sneaker already exists.";
                return;
            }

            shoe.Picture = ImageHelper.LoadShoePicture(shoe);
            _shoeRepository.Add(shoe);

            var saveResult = await _jsonShoeService.SetShoesAsync(ActiveCollectionKey, _shoeRepository.Shoes, CancellationToken);
            StatusMessage = saveResult.Success
                ? $"Sneaker added from scanner: {shoe.Brand} {shoe.Model}."
                : saveResult.Message;
        }
        catch (JsonException ex)
        {
            _logger.Error(ex, "Invalid QR code JSON.");
            StatusMessage = "Invalid QR code JSON.";
        }
        catch (Exception ex)
        {
            _logger.Error(ex, "Scanner processing error.");
            StatusMessage = "Error while processing the scan.";
        }
    }

    private async Task LoadActiveCollectionAsync()
    {
        if (!IsAuthenticated)
            return;

        IsBusy = true;
        try
        {
            var result = await _jsonShoeService.GetShoesAsync(ActiveCollectionKey, CancellationToken);
            if (!result.Success || result.Value == null)
            {
                await InitializeDefaultRemoteCollectionAsync("No remote collection found; default collection is ready.");

                return;
            }

            if (result.Value.Count == 0)
            {
                await InitializeDefaultRemoteCollectionAsync("New account initialized with the default collection.");

                return;
            }

            foreach (var shoe in result.Value)
                shoe.Picture = ImageHelper.LoadShoePicture(shoe);

            _shoeRepository.ReplaceAll(result.Value);
            StatusMessage = result.Message;
        }
        catch (Exception ex)
        {
            _logger.Error(ex, "Active collection load failed.");
            StatusMessage = "Unable to load the active collection.";
        }
        finally
        {
            IsBusy = false;
        }
    }

    private async Task InitializeDefaultRemoteCollectionAsync(string successMessage)
    {
        SeedDefaultCollection(successMessage);
        var saveResult = await _jsonShoeService.SetShoesAsync(ActiveCollectionKey, _shoeRepository.Shoes, CancellationToken);
        if (!saveResult.Success)
            StatusMessage = $"{successMessage} Remote save unavailable.";
    }

    protected override void Dispose(bool disposing)
    {
        if (!disposing)
            return;

        _scannerManager.CodeReceived -= ScannerCodeReceived;
        _scannerManager.Dispose();
        CurrentPage.Dispose();
    }
}
