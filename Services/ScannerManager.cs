using System;
using System.Collections;
using System.IO;
using System.IO.Ports;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Management;

namespace MyProjectBase.Services;

public partial class DeviceOrientationService
{
    private SerialPort? mySerialPort;
    private string? portDetected = null;
    public QueueBuffer SerialBuffer = new();
    
    public void OpenPort()
    {
        if (mySerialPort != null)
        {
            try
            {
                if (mySerialPort.IsOpen) mySerialPort.Close();
                mySerialPort.Dispose();
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Erreur lors de la fermeture du port: {ex.Message}");
            }
            finally
            {
                mySerialPort = null;
            }
        }
        else
        {
            if (OperatingSystem.IsWindows())
            {
                var searcher = new System.Management.ManagementObjectSearcher("SELECT * FROM Win32_PnPEntity WHERE Name LIKE '%(COM%'");

                foreach (System.Management.ManagementObject queryObj in searcher.Get())
                {
                    string id = queryObj["PNPDeviceID"]?.ToString() ?? "";
                    string nom = queryObj["Name"]?.ToString() ?? "";

                    if (id.Contains("PID_A4A7"))
                    {
                        int debut = nom.LastIndexOf("COM");
                        int fin = nom.LastIndexOf(")");

                        if (debut != -1 && fin != -1)
                        {
                            portDetected = nom.Substring(debut, fin - debut);
                            break;
                        }
                    }
                }
            }else if (OperatingSystem.IsLinux())
            {
                string byId = "/dev/serial/by-id";

                if (Directory.Exists(byId))
                {
                    foreach (var device in Directory.GetFiles(byId))
                    {
                        if (device.Contains("A4A7", StringComparison.OrdinalIgnoreCase))
                        {
                            portDetected = Path.GetFullPath(device);
                            break;
                        }
                    }
                }
            }

            if (portDetected != null)
            {
                mySerialPort = new SerialPort
                {
                    BaudRate = 9600,
                    PortName = portDetected,
                    Parity = Parity.None,
                    DataBits = 8,
                    StopBits = StopBits.One,
                    ReadTimeout = 10000,
                    WriteTimeout = 10000
                };

                mySerialPort.DataReceived += new SerialDataReceivedEventHandler(DataHandler);

                try
                {
                    mySerialPort.Open();
                }
                catch (Exception ex)
                {
                    
                }
            }
        }
    }
    public void ClosePort()
    {
        if (mySerialPort != null && mySerialPort.IsOpen)
        {
            try
            {
                mySerialPort.Close();
                mySerialPort.Dispose();
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Erreur lors de la fermeture du port: {ex.Message}");
            }
            finally
            {
                mySerialPort = null;
            }
        }
    }
    private void DataHandler(object sender, EventArgs arg)
    {
        SerialPort sp = (SerialPort)sender;

        SerialBuffer.Enqueue(sp.ReadExisting());
    }
    
    public sealed partial class QueueBuffer : Queue
    {
        public event EventHandler? Changed;
        public override void Enqueue(object? obj)
        { 
            base.Enqueue(obj);
            Changed?.Invoke(this,EventArgs.Empty);
        }    
    }
}