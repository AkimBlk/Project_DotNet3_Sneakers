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
        // Recupere la fenetre principale pour ouvrir une confirmation modale au-dessus de l'application.
        if (Application.Current?.ApplicationLifetime is not IClassicDesktopStyleApplicationLifetime desktop ||
            desktop.MainWindow == null)
        {
            return false;
        }

        // ConfirmWindow contient seulement le message et les boutons Yes/No.
        var window = new ConfirmWindow(message);
        return await window.ShowDialog<bool>(desktop.MainWindow);
    }
}
