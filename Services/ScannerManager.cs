using System;
using System.Collections;
using System.IO;
using System.IO.Ports;
using System.Linq;

namespace MyProjectBase.Services;

public class ScannerManager
{
    private SerialPort? _serialPort;
    private string? _portDetected;
    private string _buffer = string.Empty;

    public QueueBuffer SerialBuffer { get; } = new();

    public bool IsConnected => _serialPort is { IsOpen: true };

    public void OpenPort()
    {
        ClosePort();
        _portDetected = DetectPort();

        if (string.IsNullOrWhiteSpace(_portDetected))
        {
            throw new InvalidOperationException("Aucun scanner série détecté.");
        }

        _serialPort = new SerialPort
        {
            BaudRate = 9600,
            PortName = _portDetected,
            Parity = Parity.None,
            DataBits = 8,
            StopBits = StopBits.One,
            Handshake = Handshake.None,
            ReadTimeout = 10000,
            WriteTimeout = 10000
        };

        _serialPort.DataReceived += DataHandler;
        _serialPort.Open();
    }

    public void ClosePort()
    {
        if (_serialPort == null)
            return;

        try
        {
            if (_serialPort.IsOpen)
            {
                _serialPort.DataReceived -= DataHandler;
                _serialPort.Close();
            }

            _serialPort.Dispose();
        }
        catch (Exception ex)
        {
            throw new InvalidOperationException($"Erreur fermeture port: {ex.Message}", ex);
        }
        finally
        {
            _serialPort = null;
            _buffer = string.Empty;
        }
    }

    private string? DetectPort()
    {
        var ports = SerialPort.GetPortNames()
            .OrderBy(p => p)
            .ToArray();

        var preferredPort = ports.FirstOrDefault(p =>
            p.Contains("ttyACM", StringComparison.OrdinalIgnoreCase) ||
            p.Contains("ttyUSB", StringComparison.OrdinalIgnoreCase) ||
            p.Contains("COM", StringComparison.OrdinalIgnoreCase));

        if (!string.IsNullOrWhiteSpace(preferredPort))
            return preferredPort;

        if (OperatingSystem.IsLinux())
        {
            if (Directory.Exists("/dev"))
            {
                var linuxPorts = Directory.GetFiles("/dev", "ttyACM*")
                    .Concat(Directory.GetFiles("/dev", "ttyUSB*"))
                    .OrderBy(p => p)
                    .ToArray();

                if (linuxPorts.Length > 0)
                    return linuxPorts[0];
            }

            const string byId = "/dev/serial/by-id";
            if (Directory.Exists(byId))
            {
                var linkedPorts = Directory.GetFiles(byId).OrderBy(p => p).ToArray();
                if (linkedPorts.Length > 0)
                    return linkedPorts[0];
            }
        }

        if (OperatingSystem.IsWindows())
        {
#if WINDOWS
            try
            {
                var searcher = new System.Management.ManagementObjectSearcher(
                    "SELECT * FROM Win32_PnPEntity WHERE Name LIKE '%(COM%'");

                foreach (System.Management.ManagementObject queryObj in searcher.Get())
                {
                    string nom = queryObj["Name"]?.ToString() ?? string.Empty;

                    int debut = nom.LastIndexOf("COM", StringComparison.OrdinalIgnoreCase);
                    int fin = nom.LastIndexOf(")", StringComparison.OrdinalIgnoreCase);

                    if (debut != -1 && fin != -1)
                        return nom.Substring(debut, fin - debut);
                }
            }
            catch
            {
            }
#endif
        }

        return null;
    }

    private void DataHandler(object? sender, EventArgs e)
    {
        if (sender is not SerialPort sp)
            return;

        _buffer += sp.ReadExisting();

        int start = _buffer.IndexOf('{');
        int end = _buffer.LastIndexOf('}');

        if (start != -1 && end != -1 && end > start)
        {
            string jsonComplet = _buffer.Substring(start, end - start + 1);
            _buffer = string.Empty;
            SerialBuffer.Enqueue(jsonComplet);
            return;
        }

        if (!_buffer.Contains('{') &&
            (_buffer.EndsWith("\n") || _buffer.EndsWith("\r") || _buffer.Length > 30))
        {
            var toSend = _buffer.Trim();
            _buffer = string.Empty;

            if (!string.IsNullOrWhiteSpace(toSend))
            {
                SerialBuffer.Enqueue(toSend);
            }
        }
    }

    public sealed class QueueBuffer : Queue
    {
        public event EventHandler? Changed;

        public override void Enqueue(object? obj)
        {
            base.Enqueue(obj);
            Changed?.Invoke(this, EventArgs.Empty);
        }
    }
}