using System.IO.Ports;
using System.Linq;

namespace MyProjectBase.Services;

public interface IScannerManager : IDisposable
{
    event EventHandler<string>? CodeReceived;
    bool IsConnected { get; }
    ServiceResult OpenPort();
    ServiceResult ClosePort();
}

public sealed class ScannerManager : IScannerManager
{
    private readonly IAppLogger _logger;
    private SerialPort? _serialPort;
    private string _buffer = string.Empty;

    public event EventHandler<string>? CodeReceived;

    public bool IsConnected => _serialPort is { IsOpen: true };

    public ScannerManager(IAppLogger logger)
    {
        _logger = logger;
    }

    public ServiceResult OpenPort()
    {
        ClosePort();

        var port = DetectPort();
        if (string.IsNullOrWhiteSpace(port))
            return ServiceResult.Fail("No serial scanner detected. Check USB and Linux permissions for /dev/tty*.");

        try
        {
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

            return ServiceResult.Ok($"Scanner connected on {port}.");
        }
        catch (UnauthorizedAccessException ex)
        {
            _logger.Error(ex, "Serial port permission denied.");
            DisposePort();
            return ServiceResult.Fail("Serial port access denied. On Linux, add the user to the dialout/uucp group.");
        }
        catch (Exception ex)
        {
            _logger.Error(ex, "Scanner connection failed.");
            DisposePort();
            return ServiceResult.Fail($"Scanner connection failed: {ex.Message}");
        }
    }

    public ServiceResult ClosePort()
    {
        try
        {
            DisposePort();
            _buffer = string.Empty;
            return ServiceResult.Ok("Scanner disconnected.");
        }
        catch (Exception ex)
        {
            _logger.Error(ex, "Scanner disconnection failed.");
            return ServiceResult.Fail($"Scanner disconnection failed: {ex.Message}");
        }
    }

    public void Dispose() => ClosePort();

    private static string? DetectPort()
    {
        var ports = SerialPort.GetPortNames()
            .OrderBy(p => p)
            .ToArray();

        var preferredPort = ports.FirstOrDefault(p =>
            p.Contains("ttyACM", StringComparison.OrdinalIgnoreCase) ||
            p.Contains("ttyUSB", StringComparison.OrdinalIgnoreCase) ||
            p.Contains("cu.usb", StringComparison.OrdinalIgnoreCase) ||
            p.Contains("COM", StringComparison.OrdinalIgnoreCase));

        if (!string.IsNullOrWhiteSpace(preferredPort))
            return preferredPort;

        if (!OperatingSystem.IsLinux() || !Directory.Exists("/dev"))
            return null;

        var linuxPorts = Directory.GetFiles("/dev", "ttyACM*")
            .Concat(Directory.GetFiles("/dev", "ttyUSB*"))
            .OrderBy(p => p)
            .ToArray();

        if (linuxPorts.Length > 0)
            return linuxPorts[0];

        const string byId = "/dev/serial/by-id";
        return Directory.Exists(byId)
            ? Directory.GetFiles(byId).OrderBy(p => p).FirstOrDefault()
            : null;
    }

    private void DataHandler(object? sender, SerialDataReceivedEventArgs e)
    {
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
            ClosePort();
        }
        catch (InvalidOperationException ex)
        {
            _logger.Error(ex, "Scanner port closed while reading.");
            ClosePort();
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

    private IEnumerable<string> ExtractCodes()
    {
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
                var text = _buffer.Trim();
                _buffer = string.Empty;

                if (!string.IsNullOrWhiteSpace(text))
                    yield return text;
            }

            yield break;
        }
    }

    private void DisposePort()
    {
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
