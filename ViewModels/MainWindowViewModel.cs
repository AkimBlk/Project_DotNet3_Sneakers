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
    // MainWindowViewModel coordonne la session, la navigation et les services partages.
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
        // Les dependances sont injectees pour garder le ViewModel testable et separer l'UI des services.
        _shoeRepository = shoeRepository;
        _userRepository = userRepository;
        _jsonShoeService = jsonShoeService;
        _csvService = csvService;
        _dialogService = dialogService;
        _scannerManager = scannerManager;
        _logger = logger;

        _scannerManager.CodeReceived += ScannerCodeReceived;
        _scannerManager.ConnectionChanged += ScannerConnectionChanged;
        // Collection locale de depart avant connexion utilisateur ou chargement JSON distant.
        SeedDefaultCollection("Default collection loaded.");
        _currentPage = CreateCollectionViewModel();
    }

    public Task InitializeAsync()
    {
        // L'initialisation explicite evite de lancer un travail asynchrone lourd dans le constructeur.
        StartScannerDetection();
        return Task.CompletedTask;
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
        // Connexion MongoDB via le repository ; le ViewModel ne verifie jamais le mot de passe lui-meme.
        IsBusy = true;
        try
        {
            var result = await _userRepository.LoginAsync(LoginEmail, LoginPassword);
            await ApplyUserResultAsync(result);
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
        // Creation d'un compte utilisateur puis chargement de sa collection privee.
        IsBusy = true;
        try
        {
            var result = await _userRepository.RegisterAsync(RegisterEmail, RegisterDisplayName, RegisterPassword);
            await ApplyUserResultAsync(result);
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
    private async Task GoToAdminAsync()
    {
        // Protection importante : meme si quelqu'un essaie d'appeler la commande directement, le role est reverifie ici.
        if (!IsAdmin)
        {
            StatusMessage = "Admin access required.";
            return;
        }

        // Cette ligne cree la page admin et lui donne les services necessaires pour manipuler les utilisateurs.
        var adminPage = new AdminUsersViewModel(_userRepository, _dialogService, _logger, CurrentUser!.Id);

        // CurrentPage est affiche par le TransitioningContentControl de MainWindow.axaml.
        CurrentPage = adminPage;
        await adminPage.InitializeAsync();
        StatusMessage = "Administration displayed.";
    }

    [RelayCommand]
    private void GoToDetailsFromChild(string shoeId)
    {
        // Cette commande est appelee par le bouton Details dans CollectionView.axaml.
        var shoe = _shoeRepository.FindById(shoeId);
        if (shoe == null)
        {
            StatusMessage = "Sneaker not found.";
            return;
        }

        // Ici on remplace la page collection par la page details.
        CurrentPage = new CollectionDetailsViewModel(shoe, BackToMainCommand);
    }

    [RelayCommand]
    private void BackToMain()
    {
        // Recree la page collection principale quand on quitte details ou admin.
        CurrentPage = CreateCollectionViewModel();
        StatusMessage = "Collection displayed.";
    }

    [RelayCommand]
    private void ConnectScanner()
    {
        StartScannerDetection();
    }

    private void StartScannerDetection()
    {
        // Demarre la detection automatique : si le scanner est branche plus tard, il sera detecte.
        var result = _scannerManager.StartAutoDetection();
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
        // La cle de collection change selon l'utilisateur actif : "admin" ou l'id du user.
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

    private async Task ApplyUserResultAsync(ServiceResult<UserAccount> result)
    {
        // Applique une connexion/inscription reussie et charge la collection distante de ce compte.
        if (result.Success && result.Value != null)
        {
            CurrentUser = result.Value;
            CurrentPage = CreateCollectionViewModel();
            await LoadActiveCollectionAsync();
            LoginEmail = string.Empty;
            RegisterEmail = string.Empty;
            RegisterDisplayName = string.Empty;
        }

        StatusMessage = result.Message;
    }

    private void ScannerCodeReceived(object? sender, string raw)
    {
        // Les evenements SerialPort arrivent hors thread UI : on repasse par Dispatcher avant de modifier l'interface.
        Dispatcher.UIThread.InvokeAsync(() => HandleScanAsync(raw));
    }

    private void ScannerConnectionChanged(object? sender, ServiceResult result)
    {
        // Met a jour l'etat visible quand le scanner est branche ou debranche.
        Dispatcher.UIThread.Post(() =>
        {
            IsScannerConnected = _scannerManager.IsConnected;
            StatusMessage = result.Message;
        });
    }

    private async Task HandleScanAsync(string raw)
    {
        // Un scan peut etre soit un ID simple, soit un JSON complet representant une sneaker.
        if (string.IsNullOrWhiteSpace(raw))
            return;

        QrCode = raw.Length > 80 ? $"{raw[..80]}..." : raw;

        var existingByRawId = _shoeRepository.FindById(raw);
        if (existingByRawId != null)
        {
            // Si l'ID existe deja dans la collection, on ouvre directement le detail.
            CurrentPage = new CollectionDetailsViewModel(existingByRawId, BackToMainCommand);
            StatusMessage = "Sneaker found from scan.";
            return;
        }

        if (CurrentPage is CollectionViewModel { IsAddPanelVisible: true } collectionViewModel &&
            !raw.TrimStart().StartsWith("{", StringComparison.Ordinal))
        {
            // Si l'utilisateur est en train d'ajouter une sneaker, un ID simple remplit le champ identifier.
            collectionViewModel.UseScannedId(raw);
            StatusMessage = "Scanned ID copied into the add form.";
            return;
        }

        if (!raw.TrimStart().StartsWith("{", StringComparison.Ordinal))
        {
            StatusMessage = "Scanned ID not found. Open Add to use it as a new sneaker identifier.";
            return;
        }

        try
        {
            // Si le scan est un JSON, on tente de construire une nouvelle sneaker.
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
                // Evite les doublons si le meme QR code est scanne plusieurs fois.
                CurrentPage = new CollectionDetailsViewModel(duplicate, BackToMainCommand);
                StatusMessage = "Sneaker already exists.";
                return;
            }

            shoe.Picture = ImageHelper.LoadShoePicture(shoe);
            _shoeRepository.Add(shoe);

            // Toute modification issue du scanner est sauvegardee dans la collection JSON de l'utilisateur actif.
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
        // Charge la collection JSON correspondant au compte connecte.
        if (!IsAuthenticated)
            return;

        IsBusy = true;
        try
        {
            var result = await _jsonShoeService.GetShoesAsync(ActiveCollectionKey, CancellationToken);
            if (!result.Success || result.Value == null)
            {
                // Une collection inexistante est initialisee ; une panne reseau est simplement affichee.
                if (result.Kind == ServiceResultKind.NotFound)
                    await InitializeDefaultRemoteCollectionAsync("No remote collection found; default collection is ready.");
                else
                    StatusMessage = result.Message;
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
        // Cree une collection de depart pour un nouvel utilisateur et tente de l'envoyer au serveur.
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
        _scannerManager.ConnectionChanged -= ScannerConnectionChanged;
        _scannerManager.Dispose();
        CurrentPage.Dispose();
    }
}
