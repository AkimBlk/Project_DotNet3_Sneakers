using MyProjectBase.Repositories;
using MyProjectBase.ViewModels;

namespace MyProjectBase.Services;

public sealed class AppServices : IDisposable
{
    public IAppLogger Logger { get; }
    public IShoeRepository ShoeRepository { get; }
    public IUserRepository UserRepository { get; }
    public IJsonShoeService JsonShoeService { get; }
    public ICsvService CsvService { get; }
    public IDialogService DialogService { get; }
    public IScannerManager ScannerManager { get; }

    public AppServices()
    {
        Logger = new AppLogger();
        ShoeRepository = new ShoeRepository();
        UserRepository = new MongoUserRepository(Logger);
        JsonShoeService = new JsonShoeService(Logger);
        CsvService = new CsvService(Logger);
        DialogService = new DialogService();
        ScannerManager = new ScannerManager(Logger);
    }

    public MainWindowViewModel CreateMainWindowViewModel()
    {
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
        ScannerManager.Dispose();
    }
}
