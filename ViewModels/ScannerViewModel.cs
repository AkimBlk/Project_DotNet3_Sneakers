using System.Collections.ObjectModel;
using Avalonia.Threading;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using MyProjectBase.Services;

namespace MyProjectBase.ViewModels;

public partial class ScannerViewModel : ViewModelBase
{
    // Page scanner inspiree du projet de base, adaptee ici a la detection automatique sans afficher les ports.
    private readonly IScannerManager _scannerManager;

    [ObservableProperty] private string _status = "Automatic scanner detection is running.";
    [ObservableProperty] private string _lastScan = "No scan";
    [ObservableProperty] private bool _isConnected;

    // Historique visible des codes lus pendant la session.
    public ObservableCollection<string> Scans { get; } = [];

    public ScannerViewModel()
        : this(new ScannerManager(new AppLogger()))
    {
    }

    public ScannerViewModel(IScannerManager scannerManager)
    {
        _scannerManager = scannerManager;
        _scannerManager.CodeReceived += OnCodeReceived;
        _scannerManager.ConnectionChanged += OnConnectionChanged;

        IsConnected = _scannerManager.IsConnected;
    }

    [RelayCommand]
    private void StartDetection()
    {
        // Relance la detection automatique si l'utilisateur a ferme le scanner manuellement.
        var result = _scannerManager.StartAutoDetection();
        IsConnected = _scannerManager.IsConnected;
        Status = result.Message;
    }

    [RelayCommand]
    private void Disconnect()
    {
        // Ferme le port serie et arrete la detection automatique.
        var result = _scannerManager.ClosePort();
        IsConnected = _scannerManager.IsConnected;
        Status = result.Message;
    }

    [RelayCommand]
    private void ClearHistory()
    {
        // Nettoie seulement l'affichage local, pas les donnees de collection.
        Scans.Clear();
        LastScan = "No scan";
    }

    private void OnCodeReceived(object? sender, string code)
    {
        // Les evenements du scanner arrivent hors thread UI.
        Dispatcher.UIThread.Post(() =>
        {
            LastScan = code;
            Scans.Insert(0, $"{DateTime.Now:HH:mm:ss}  {code}");
        });
    }

    private void OnConnectionChanged(object? sender, ServiceResult result)
    {
        Dispatcher.UIThread.Post(() =>
        {
            IsConnected = _scannerManager.IsConnected;
            Status = result.Message;
        });
    }

    protected override void Dispose(bool disposing)
    {
        if (!disposing)
            return;

        _scannerManager.CodeReceived -= OnCodeReceived;
        _scannerManager.ConnectionChanged -= OnConnectionChanged;
    }
}
