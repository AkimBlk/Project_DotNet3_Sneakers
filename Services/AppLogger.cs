using System;
using System.IO;
using System.Threading.Tasks;

namespace MyProjectBase.Services;

public interface IAppLogger
{
    void Info(string message);
    void Error(Exception exception, string message);
}

public sealed class AppLogger : IAppLogger
{
    private readonly string _logPath;
    private readonly object _sync = new();

    public AppLogger()
    {
        var directory = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
            "SneakerCollection",
            "Logs");

        Directory.CreateDirectory(directory);
        _logPath = Path.Combine(directory, "app.log");
    }

    public void Info(string message) => Write("INFO", message);

    public void Error(Exception exception, string message)
    {
        Write("ERROR", $"{message}{Environment.NewLine}{exception}");
    }

    private void Write(string level, string message)
    {
        var line = $"{DateTimeOffset.Now:O} [{level}] {message}{Environment.NewLine}";

        lock (_sync)
        {
            File.AppendAllText(_logPath, line);
        }
    }
}
