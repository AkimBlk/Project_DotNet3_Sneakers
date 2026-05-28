using Avalonia;
using System;

namespace MyProjectBase;

sealed class Program
{
    // Code d'initialisation : ne pas utiliser Avalonia ou des API liees au contexte UI avant AppMain.
    [STAThread]
    public static void Main(string[] args) => BuildAvaloniaApp()
        .StartWithClassicDesktopLifetime(args);

    // Configuration Avalonia utilisee aussi par le designer visuel.
    public static AppBuilder BuildAvaloniaApp()
        => AppBuilder.Configure<App>()
            .UsePlatformDetect()
            .WithInterFont()
            .LogToTrace();
}
