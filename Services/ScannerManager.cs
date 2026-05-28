using System.IO.Ports;
using System.Linq;

namespace MyProjectBase.Services;

public interface IScannerManager : IDisposable
{
    event EventHandler<string>? CodeReceived;
    event EventHandler<ServiceResult>? ConnectionChanged;
    bool IsConnected { get; }
    ServiceResult StartAutoDetection();
    ServiceResult StopAutoDetection();
    ServiceResult OpenPort();
    ServiceResult ClosePort();
}

public sealed class ScannerManager : IScannerManager
{
    private readonly IAppLogger _logger;

    // Verrou utilise parce que le Timer, les evenements SerialPort et l'UI peuvent appeler ce service en meme temps.
    private readonly object _sync = new();
    private Timer? _autoDetectionTimer;
    private SerialPort? _serialPort;

    // Buffer temporaire : un scanner peut envoyer le contenu en plusieurs petits morceaux.
    private string _buffer = string.Empty;
    private bool _isDetecting;
    private bool? _lastReportedConnectionState;
    private string? _lastReportedMessage;

    public event EventHandler<string>? CodeReceived;
    public event EventHandler<ServiceResult>? ConnectionChanged;

    public bool IsConnected
    {
        get
        {
            lock (_sync)
                return _serialPort is { IsOpen: true };
        }
    }

    public ScannerManager(IAppLogger logger)
    {
        _logger = logger;
    }

    public ServiceResult StartAutoDetection()
    {
        // Lance une surveillance legere : si le scanner est branche apres le demarrage, il sera connecte automatiquement.
        lock (_sync)
        {
            _autoDetectionTimer ??= new Timer(AutoDetectScanner, null, TimeSpan.Zero, TimeSpan.FromSeconds(2));
        }

        return ServiceResult.Ok("Automatic scanner detection started.");
    }

    public ServiceResult StopAutoDetection()
    {
        // Arrete uniquement la surveillance automatique, pas la logique de lecture deja recue.
        lock (_sync)
        {
            _autoDetectionTimer?.Dispose();
            _autoDetectionTimer = null;
        }

        return ServiceResult.Ok("Automatic scanner detection stopped.");
    }

    public ServiceResult OpenPort()
    {
        // Methode publique appelee par le ViewModel quand l'utilisateur veut relancer la connexion.
        ServiceResult result;

        lock (_sync)
        {
            result = OpenPortLocked();
        }

        NotifyConnection(result);
        return result;
    }

    private ServiceResult OpenPortLocked()
    {
        // Cette methode suppose que _sync est deja verrouille par l'appelant.
        if (_serialPort is { IsOpen: true })
            return ServiceResult.Ok("Scanner already connected.");

        DisposePortLocked();
        _buffer = string.Empty;

        var port = DetectPort();
        if (string.IsNullOrWhiteSpace(port))
            return ServiceResult.Unavailable("Waiting for scanner connection.");

        try
        {
            // Parametres imposes par le cahier des charges pour le scanner/code-barres.
            _serialPort = new SerialPort(port)
            {
                BaudRate = 9600,
                Parity = Parity.None,
                DataBits = 8,
                StopBits = StopBits.One,
                Handshake = Handshake.None,
                ReadTimeout = 3000,
                WriteTimeout = 3000,
                NewLine = "\n"
            };

            _serialPort.DataReceived += DataHandler;
            _serialPort.ErrorReceived += ErrorHandler;
            _serialPort.Open();

            return ServiceResult.Ok("Scanner detected and connected automatically.");
        }
        catch (UnauthorizedAccessException ex)
        {
            _logger.Error(ex, "Serial port permission denied.");
            DisposePortLocked();
            return ServiceResult.Fail("Serial port access denied. On Linux, add the user to the dialout/uucp group.");
        }
        catch (Exception ex)
        {
            _logger.Error(ex, "Scanner connection failed.");
            DisposePortLocked();
            return ServiceResult.Fail($"Scanner connection failed: {ex.Message}");
        }
    }

    public ServiceResult ClosePort()
    {
        // Un disconnect manuel arrete aussi l'auto-detection pour ne pas reconnecter juste apres.
        StopAutoDetection();

        try
        {
            lock (_sync)
            {
                DisposePortLocked();
                _buffer = string.Empty;
            }

            NotifyConnection(ServiceResult.Ok("Scanner disconnected."));
            return ServiceResult.Ok("Scanner disconnected.");
        }
        catch (Exception ex)
        {
            _logger.Error(ex, "Scanner disconnection failed.");
            return ServiceResult.Fail($"Scanner disconnection failed: {ex.Message}");
        }
    }

    public void Dispose()
    {
        StopAutoDetection();
        ClosePort();
    }

    private void AutoDetectScanner(object? state)
    {
        // Le Timer peut se declencher alors qu'une detection precedente est encore en cours.
        lock (_sync)
        {
            if (_isDetecting || _serialPort is { IsOpen: true })
                return;

            _isDetecting = true;
        }

        ServiceResult result;
        try
        {
            lock (_sync)
            {
                result = OpenPortLocked();
            }
        }
        finally
        {
            lock (_sync)
                _isDetecting = false;
        }

        NotifyConnection(result);
    }

    private void NotifyConnection(ServiceResult result)
    {
        // Evite d'envoyer en boucle le meme message a l'interface toutes les deux secondes.
        var connected = IsConnected;

        if (_lastReportedConnectionState == connected && _lastReportedMessage == result.Message)
            return;

        _lastReportedConnectionState = connected;
        _lastReportedMessage = result.Message;
        ConnectionChanged?.Invoke(this, result);
    }

    private static string? DetectPort()
    {
        // Permet de forcer un port sans l'afficher dans l'interface, utile si le scanner a un nom atypique.
        var configuredPort = Environment.GetEnvironmentVariable("SNEAKER_SCANNER_PORT");
        if (!string.IsNullOrWhiteSpace(configuredPort))
            return configuredPort.Trim();

        if (OperatingSystem.IsLinux() && Directory.Exists("/dev"))
        {
            // Sur Linux, /dev/serial/by-id donne souvent un nom stable lie au peripherique USB.
            const string byId = "/dev/serial/by-id";
            if (Directory.Exists(byId))
            {
                var knownScanner = Directory.GetFiles(byId)
                    .OrderBy(path => path)
                    .FirstOrDefault(path =>
                        path.Contains("20080411", StringComparison.OrdinalIgnoreCase) ||
                        path.Contains("M900", StringComparison.OrdinalIgnoreCase) ||
                        path.Contains("Barcode", StringComparison.OrdinalIgnoreCase) ||
                        path.Contains("Scanner", StringComparison.OrdinalIgnoreCase));

                if (!string.IsNullOrWhiteSpace(knownScanner))
                    return Path.GetFullPath(knownScanner);
            }

            var linuxPort = new[] { "/dev/ttyACM0", "/dev/ttyUSB0", "/dev/ttyACM1", "/dev/ttyUSB1" }
                .FirstOrDefault(File.Exists);

            if (!string.IsNullOrWhiteSpace(linuxPort))
                return linuxPort;
        }

        if (OperatingSystem.IsMacOS() && Directory.Exists("/dev"))
        {
            // Sur macOS, les ports USB apparaissent souvent sous /dev/cu.* ou /dev/tty.*.
            var macPort = Directory.GetFiles("/dev", "cu.usb*")
                              .Concat(Directory.GetFiles("/dev", "tty.usb*"))
                              .OrderBy(path => path)
                              .FirstOrDefault()
                          ?? Directory.GetFiles("/dev", "cu.*")
                              .OrderBy(path => path)
                              .FirstOrDefault(path =>
                                  path.Contains("usb", StringComparison.OrdinalIgnoreCase) ||
                                  path.Contains("serial", StringComparison.OrdinalIgnoreCase) ||
                                  path.Contains("modem", StringComparison.OrdinalIgnoreCase));

            if (!string.IsNullOrWhiteSpace(macPort))
                return macPort;
        }

        var ports = SerialPort.GetPortNames()
            .Where(port => !string.IsNullOrWhiteSpace(port))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .OrderBy(port => port)
            .ToArray();

        // Dernier essai multi-plateforme fourni par System.IO.Ports.
        return ports.FirstOrDefault(p =>
                   p.Contains("ttyACM", StringComparison.OrdinalIgnoreCase) ||
                   p.Contains("ttyUSB", StringComparison.OrdinalIgnoreCase) ||
                   p.Contains("cu.usb", StringComparison.OrdinalIgnoreCase) ||
                   p.Contains("COM", StringComparison.OrdinalIgnoreCase))
               ?? ports.FirstOrDefault();
    }

    private void DataHandler(object? sender, SerialDataReceivedEventArgs e)
    {
        // Evenement appele par SerialPort quand des caracteres sont disponibles.
        if (sender is not SerialPort serialPort)
            return;

        try
        {
            _buffer += serialPort.ReadExisting();
            foreach (var code in ExtractCodes())
                CodeReceived?.Invoke(this, code);
        }
        catch (IOException ex)
        {
            _logger.Error(ex, "Scanner disconnected while reading.");
            ClosePortAfterReadError();
        }
        catch (InvalidOperationException ex)
        {
            _logger.Error(ex, "Scanner port closed while reading.");
            ClosePortAfterReadError();
        }
        catch (Exception ex)
        {
            _logger.Error(ex, "Scanner read error.");
        }
    }

    private void ErrorHandler(object sender, SerialErrorReceivedEventArgs e)
    {
        _logger.Info($"Serial port error received: {e.EventType}");
    }

    private void ClosePortAfterReadError()
    {
        // Si le scanner est debranche pendant une lecture, on ferme le port pour permettre une future reconnexion.
        lock (_sync)
        {
            DisposePortLocked();
            _buffer = string.Empty;
        }

        NotifyConnection(ServiceResult.Unavailable("Scanner disconnected. Waiting for reconnection."));
    }

    private IEnumerable<string> ExtractCodes()
    {
        // Cas principal : le QR contient un JSON complet entre accolades.
        while (true)
        {
            var start = _buffer.IndexOf('{', StringComparison.Ordinal);
            var end = _buffer.IndexOf('}', Math.Max(start, 0));

            if (start >= 0 && end > start)
            {
                var json = _buffer.Substring(start, end - start + 1).Trim();
                _buffer = _buffer[(end + 1)..];
                yield return json;
                continue;
            }

            if (!_buffer.Contains('{', StringComparison.Ordinal) &&
                (_buffer.Contains('\n', StringComparison.Ordinal) ||
                 _buffer.Contains('\r', StringComparison.Ordinal) ||
                 _buffer.Length > 64))
            {
                // Cas secondaire : le scanner envoie un ID simple au lieu d'un JSON.
                var text = _buffer.Trim();
                _buffer = string.Empty;

                if (!string.IsNullOrWhiteSpace(text))
                    yield return text;
            }

            yield break;
        }
    }

    private void DisposePortLocked()
    {
        // Toujours se desabonner des evenements avant de detruire le port.
        if (_serialPort == null)
            return;

        _serialPort.DataReceived -= DataHandler;
        _serialPort.ErrorReceived -= ErrorHandler;

        if (_serialPort.IsOpen)
            _serialPort.Close();

        _serialPort.Dispose();
        _serialPort = null;
    }
}
