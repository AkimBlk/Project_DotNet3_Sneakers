using System;
using System.Linq;
using System.Text.Json;
using Avalonia.Threading;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using MyProjectBase.Helpers;
using MyProjectBase.Models;
using MyProjectBase.Services;

namespace MyProjectBase.ViewModels;

public partial class MainWindowViewModel : ViewModelBase
{
    private readonly JSONServices _jsonServices;
    private readonly ScannerManager _myScanner;

    [ObservableProperty]
    private ViewModelBase _currentPage;

    [ObservableProperty]
    private string _qrCode = "No scan";

    [ObservableProperty]
    private string _errorMessage = string.Empty;

    [ObservableProperty]
    private bool _isScannerConnected;

    public MainWindowViewModel()
    {
        _jsonServices = new JSONServices();
        _myScanner = new ScannerManager();

        _myScanner.SerialBuffer.Changed += QRCodeManager;

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

        if (CurrentPage is CollectionViewModel collectionPage)
        {
            foreach (var shoe in MyGlobals.MyShoes)
            {
                if (!collectionPage.MyObservableShoes.Any(s => s.Id == shoe.Id))
                {
                    collectionPage.MyObservableShoes.Add(shoe);
                }
            }
        }
    }

    [RelayCommand]
    private void ConnectScanner()
    {
        try
        {
            _myScanner.OpenPort();
            IsScannerConnected = true;
            ErrorMessage = "Scanner connected.";
        }
        catch (Exception ex)
        {
            IsScannerConnected = false;
            ErrorMessage = $"Scanner connection failed: {ex.Message}";
        }
    }

    [RelayCommand]
    private void DisconnectScanner()
    {
        try
        {
            _myScanner.ClosePort();
            IsScannerConnected = false;
            ErrorMessage = "Scanner disconnected.";
        }
        catch (Exception ex)
        {
            ErrorMessage = $"Disconnection error: {ex.Message}";
        }
    }

    private async void QRCodeManager(object? sender, EventArgs e)
    {
        var raw = _myScanner.SerialBuffer.Count > 0
            ? _myScanner.SerialBuffer.Dequeue()?.ToString() ?? string.Empty
            : string.Empty;

        if (string.IsNullOrWhiteSpace(raw))
            return;

        try
        {
            var preview = JsonSerializer.Deserialize<Shoe>(raw,
                new JsonSerializerOptions { PropertyNameCaseInsensitive = true });

            QrCode = !string.IsNullOrWhiteSpace(preview?.Model)
                ? $"{preview.Brand} {preview.Model}".Trim()
                : raw;
        }
        catch
        {
            QrCode = raw;
        }

        var existingByRawId = MyGlobals.MyShoes
            .FirstOrDefault(s => s.Id.Equals(raw, StringComparison.OrdinalIgnoreCase));

        if (existingByRawId != null)
        {
            Dispatcher.UIThread.Post(() =>
                CurrentPage = new CollectionDetailsViewModel(existingByRawId.Id));
            return;
        }

        try
        {
            var shoe = JsonSerializer.Deserialize<Shoe>(raw,
                new JsonSerializerOptions { PropertyNameCaseInsensitive = true });

            if (shoe != null &&
                (!string.IsNullOrWhiteSpace(shoe.Brand) || !string.IsNullOrWhiteSpace(shoe.Model)))
            {
                if (!string.IsNullOrWhiteSpace(shoe.Id))
                {
                    var existingById = MyGlobals.MyShoes.FirstOrDefault(s =>
                        s.Id.Equals(shoe.Id, StringComparison.OrdinalIgnoreCase));

                    if (existingById != null)
                    {
                        Dispatcher.UIThread.Post(() =>
                            CurrentPage = new CollectionDetailsViewModel(existingById.Id));
                        return;
                    }
                }

                var existingByBrandModel = MyGlobals.MyShoes.FirstOrDefault(s =>
                    s.Brand.Equals(shoe.Brand ?? string.Empty, StringComparison.OrdinalIgnoreCase) &&
                    s.Model.Equals(shoe.Model ?? string.Empty, StringComparison.OrdinalIgnoreCase));

                if (existingByBrandModel != null)
                {
                    Dispatcher.UIThread.Post(() =>
                        CurrentPage = new CollectionDetailsViewModel(existingByBrandModel.Id));
                    return;
                }

                if (string.IsNullOrWhiteSpace(shoe.Id))
                    shoe.Id = Guid.NewGuid().ToString();

                if (!string.IsNullOrWhiteSpace(shoe.ImagePath))
                {
                    try
                    {
                        shoe.Picture = ImageHelper.LoadFromResource(new Uri(shoe.ImagePath));
                    }
                    catch
                    {
                        shoe.Picture = null;
                    }
                }

                MyGlobals.MyShoes.Add(shoe);
                await _jsonServices.SetShoesAsync(MyGlobals.MyShoes.ToList());

                Dispatcher.UIThread.Post(() =>
                {
                    if (CurrentPage is CollectionViewModel col)
                    {
                        if (!col.MyObservableShoes.Any(s => s.Id == shoe.Id))
                            col.MyObservableShoes.Add(shoe);
                    }
                    else
                    {
                        CurrentPage = new CollectionViewModel(GoToDetailsFromChildCommand);

                        if (CurrentPage is CollectionViewModel newCol &&
                            !newCol.MyObservableShoes.Any(s => s.Id == shoe.Id))
                        {
                            newCol.MyObservableShoes.Add(shoe);
                        }
                    }

                    ErrorMessage = $"Sneaker added via scan: {shoe.Brand} {shoe.Model}".Trim();
                });

                return;
            }
        }
        catch
        {
        }

        Dispatcher.UIThread.Post(() =>
            ErrorMessage = "Unrecognized QR code. Please scan a valid QR code.");
    }
}