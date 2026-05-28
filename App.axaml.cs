using System.Linq;
using Avalonia;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Data.Core.Plugins;
using Avalonia.Markup.Xaml;
using Avalonia.Threading;
using MyProjectBase.Services;
using MyProjectBase.Views;

namespace MyProjectBase;

public class App : Application
{
    private AppServices? _services;

    public override void Initialize()
    {
        // Charge App.axaml : themes, styles et ViewLocator.
        AvaloniaXamlLoader.Load(this);
    }

    public override void OnFrameworkInitializationCompleted()
    {
        // Cree les services partages avant de creer la fenetre principale.
        _services = new AppServices();
        RegisterGlobalErrorHandlers(_services.Logger);

        if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
        {
            DisableAvaloniaDataAnnotationValidation();
            var mainViewModel = _services.CreateMainWindowViewModel();

            // DataContext relie MainWindow.axaml a MainWindowViewModel pour tous les bindings.
            desktop.MainWindow = new MainWindow
            {
                DataContext = mainViewModel
            };

            // Lance l'initialisation controlee du ViewModel apres creation des services.
            mainViewModel.InitializeAsync().GetAwaiter().GetResult();
            desktop.Exit += (_, _) => _services.Dispose();
        }

        base.OnFrameworkInitializationCompleted();
    }

    private static void RegisterGlobalErrorHandlers(IAppLogger logger)
    {
        // Ces handlers evitent qu'une exception non prevue ferme l'application sans trace.
        AppDomain.CurrentDomain.UnhandledException += (_, args) =>
        {
            if (args.ExceptionObject is Exception exception)
                logger.Error(exception, "Unhandled global exception.");
        };

        TaskScheduler.UnobservedTaskException += (_, args) =>
        {
            logger.Error(args.Exception, "Unobserved task exception.");
            args.SetObserved();
        };

        Dispatcher.UIThread.UnhandledException += (_, args) =>
        {
            logger.Error(args.Exception, "Unhandled UI exception.");
            args.Handled = true;
        };
    }

    private static void DisableAvaloniaDataAnnotationValidation()
    {
        // Evite un conflit connu entre la validation Avalonia et CommunityToolkit.Mvvm.
        var dataValidationPluginsToRemove =
            BindingPlugins.DataValidators.OfType<DataAnnotationsValidationPlugin>().ToArray();

        foreach (var plugin in dataValidationPluginsToRemove)
            BindingPlugins.DataValidators.Remove(plugin);
    }
}
