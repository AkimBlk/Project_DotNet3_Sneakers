using MyProjectBase.Repositories;
using MyProjectBase.ViewModels;

namespace MyProjectBase.Services;

public sealed class AppServices : IDisposable
{
    // Conteneur simple de dependances pour creer une seule instance partagee des services principaux.
    public IAppLogger Logger { get; }
    public IShoeRepository ShoeRepository { get; }
    public IUserRepository UserRepository { get; }
    public IJsonShoeService JsonShoeService { get; }
    public ICsvService CsvService { get; }
    public IDialogService DialogService { get; }
    public IScannerManager ScannerManager { get; }
    public AppConfiguration Configuration { get; }

    public AppServices()
    {
        // L'ordre est important : la configuration sert aux services MongoDB et JSON.
        Configuration = new AppConfiguration();
        Logger = new AppLogger();
        ShoeRepository = new ShoeRepository();
        UserRepository = new MongoUserRepository(Logger, Configuration);
        JsonShoeService = new JsonShoeService(Logger, Configuration);
        CsvService = new CsvService(Logger);
        DialogService = new DialogService();
        ScannerManager = new ScannerManager(Logger);
    }

    public MainWindowViewModel CreateMainWindowViewModel()
    {
        // Creation du ViewModel principal avec toutes les dependances necessaires.
        return new MainWindowViewModel(
            ShoeRepository,
            UserRepository,
            JsonShoeService,
            CsvService,
            DialogService,
            ScannerManager,
            Logger);
    }

    public void Dispose()
    {
        // Ferme proprement le port serie quand l'application se termine.
        ScannerManager.Dispose();
    }
}
