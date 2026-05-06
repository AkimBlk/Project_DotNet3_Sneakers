using Avalonia;
using Avalonia.Controls.ApplicationLifetimes;
using MyProjectBase.Views;

namespace MyProjectBase.Services;

public interface IDialogService
{
    Task<bool> ConfirmAsync(string message);
}

public sealed class DialogService : IDialogService
{
    public async Task<bool> ConfirmAsync(string message)
    {
        if (Application.Current?.ApplicationLifetime is not IClassicDesktopStyleApplicationLifetime desktop ||
            desktop.MainWindow == null)
        {
            return false;
        }

        var window = new ConfirmWindow(message);
        return await window.ShowDialog<bool>(desktop.MainWindow);
    }
}
