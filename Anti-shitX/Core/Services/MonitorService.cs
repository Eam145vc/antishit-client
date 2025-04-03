using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Management;
using System.Net;
using System.Net.NetworkInformation;
using System.Net.Sockets;
using System.Runtime.InteropServices;
using System.ServiceProcess;
using System.Threading.Tasks;
using Microsoft.Win32;
using Newtonsoft.Json;
using AntiCheatClient.Core.Models;

namespace AntiCheatClient.Core.Services
{
    public class MonitorService
    {
        private readonly ApiService _apiService;

        public MonitorService(ApiService apiService)
        {
            _apiService = apiService;
        }

        public async Task<bool> SendMonitorData()
        {
            try
            {
                // Verificar si hay conexión antes de intentar enviar datos
                if (!_apiService.IsConnected)
                {
                    await _apiService.CheckConnection();
                    if (!_apiService.IsConnected)
                        return false;
                }

                // Obtener IDs desde MainOverlay - implementación más segura
                string activisionId = UI.Windows.MainOverlay.CurrentActivisionId;
                int channelId = UI.Windows.MainOverlay.CurrentChannelId;

                // Verificar datos obligatorios
                if (string.IsNullOrEmpty(activisionId) || channelId <= 0)
                {
                    Console.WriteLine("Error: ActivisionId y/o ChannelId no están definidos");
                    return false;
                }

                // Detectar si el juego está en ejecución
                bool isGameRunning = IsGameRunning();

                // Obtener hora de inicio del PC
                string pcStartTime = GetLastBootTime();

                // Recopilar información del sistema y hardware
                SystemInfo systemInfo = GetSystemInfo();
                HardwareInfo hardwareInfo = GetHardwareInfo();

                // Recopilar información de dispositivos USB
                List<DeviceInfo> usbDevices = GetConnectedDevices();

                // Recopilar conexiones de red
                List<NetworkConnection> networkConnections = GetNetworkConnections();

                // Recopilar drivers cargados
                List<DriverInfo> loadedDrivers = GetLoadedDrivers();

                // Recopilar información del sistema
                MonitorData monitorData = new MonitorData
                {
                    // Estos campos son OBLIGATORIOS según el esquema del servidor
                    ActivisionId = activisionId,
                    ChannelId = channelId,

                    // Otros campos importantes
                    Timestamp = DateTime.Now,
                    ClientStartTime = UI.Windows.MainOverlay.ClientStartTime,
                    PcStartTime = pcStartTime,
                    IsGameRunning = isGameRunning,

                    // Información de dispositivos
                    UsbDevices = usbDevices,

                    // Información del sistema y hardware
                    SystemInfo = systemInfo,
                    HardwareInfo = hardwareInfo,

                    // Conexiones de red
                    NetworkConnections = networkConnections,

                    // Drivers cargados
                    LoadedDrivers = loadedDrivers
                };

                // Debuggear datos enviados
                Console.WriteLine($"Enviando systemInfo: {JsonConvert.SerializeObject(systemInfo)}");
                Console.WriteLine($"Enviando hardwareInfo: {JsonConvert.SerializeObject(hardwareInfo)}");

                // Enviar datos al servidor
                bool monitorResult = await _apiService.SendMonitorData(monitorData);

                // También enviamos el estado del juego separadamente
                await _apiService.SendGameStatus(activisionId, channelId, isGameRunning);

                return monitorResult;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error enviando datos de monitoreo: {ex.Message}");
                return false;
            }
        }

        public bool IsGameRunning()
        {
            try
            {
                // Lista de nombres de procesos que representan el juego
                string[] gameProcessNames = { "ModernWarfare", "BlackOpsColdWar", "Warzone", "Vanguard", "MW2" };

                Process[] processes = Process.GetProcesses();

                foreach (Process process in processes)
                {
                    try
                    {
                        foreach (string gameName in gameProcessNames)
                        {
                            if (process.ProcessName.IndexOf(gameName, StringComparison.OrdinalIgnoreCase) >= 0)
                            {
                                return true;
                            }
                        }
                    }
                    catch
                    {
                        // Ignorar errores al acceder a un proceso específico
                    }
                }

                return false;
            }
            catch
            {
                return false;
            }
        }

        public SystemInfo GetSystemInfo()
        {
            SystemInfo systemInfo = new SystemInfo();

            try
            {
                // Obtener versión de Windows
                using (ManagementObjectSearcher searcher = new ManagementObjectSearcher(
                    "SELECT Caption, Version, InstallDate FROM Win32_OperatingSystem"))
                {
                    foreach (ManagementObject os in searcher.Get())
                    {
                        systemInfo.WindowsVersion = $"{os["Caption"]} {os["Version"]}";

                        if (os["InstallDate"] != null)
                        {
                            // Convertir fecha de instalación
                            try
                            {
                                var installDate = ManagementDateTimeConverter.ToDateTime(os["InstallDate"].ToString());
                                systemInfo.WindowsInstallDate = installDate.ToString("yyyy-MM-dd");
                            }
                            catch
                            {
                                systemInfo.WindowsInstallDate = "Unknown";
                            }
                        }

                        break; // Solo necesitamos el primero
                    }
                }

                // Obtener versión de DirectX
                try
                {
                    using (RegistryKey key = Registry.LocalMachine.OpenSubKey(
                        @"SOFTWARE\Microsoft\DirectX", false))
                    {
                        if (key != null)
                        {
                            systemInfo.DirectXVersion = key.GetValue("Version")?.ToString() ?? "Unknown";
                        }
                    }
                }
                catch
                {
                    systemInfo.DirectXVersion = "Unknown";
                }

                // Obtener resolución de pantalla
                systemInfo.ScreenResolution = $"{System.Windows.SystemParameters.PrimaryScreenWidth}x{System.Windows.SystemParameters.PrimaryScreenHeight}";

                // Usuario de Windows y nombre de PC
                systemInfo.WindowsUsername = Environment.UserName;
                systemInfo.ComputerName = Environment.MachineName;

                // Último tiempo de arranque
                systemInfo.LastBootTime = GetLastBootTime();

                // Zona horaria
                systemInfo.TimeZone = TimeZone.CurrentTimeZone.StandardName;

                // Versión del framework .NET
                systemInfo.FrameworkVersion = RuntimeInformation.FrameworkDescription;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error obteniendo información del sistema: {ex.Message}");
            }

            return systemInfo;
        }

        public HardwareInfo GetHardwareInfo()
        {
            HardwareInfo hardwareInfo = new HardwareInfo();

            try
            {
                // Obtener información de CPU
                using (ManagementObjectSearcher cpuSearcher = new ManagementObjectSearcher(
                    "SELECT Name FROM Win32_Processor"))
                {
                    foreach (ManagementObject cpu in cpuSearcher.Get())
                    {
                        hardwareInfo.Cpu = cpu["Name"]?.ToString() ?? "Unknown CPU";
                        break; // Solo tomamos el primero
                    }
                }

                // Obtener información de GPU
                using (ManagementObjectSearcher gpuSearcher = new ManagementObjectSearcher(
                    "SELECT Name, DriverVersion FROM Win32_VideoController"))
                {
                    foreach (ManagementObject gpu in gpuSearcher.Get())
                    {
                        hardwareInfo.Gpu = gpu["Name"]?.ToString() ?? "Unknown GPU";
                        hardwareInfo.GpuDriverVersion = gpu["DriverVersion"]?.ToString() ?? "Unknown";
                        break; // Solo tomamos el primero
                    }
                }

                // Obtener información de RAM
                using (ManagementObjectSearcher ramSearcher = new ManagementObjectSearcher(
                    "SELECT Capacity FROM Win32_PhysicalMemory"))
                {
                    ulong totalMemory = 0;
                    foreach (ManagementObject ram in ramSearcher.Get())
                    {
                        if (ram["Capacity"] != null)
                        {
                            totalMemory += Convert.ToUInt64(ram["Capacity"]);
                        }
                    }

                    hardwareInfo.Ram = $"{totalMemory / (1024 * 1024 * 1024)} GB";
                }

                // Obtener información de placa base
                using (ManagementObjectSearcher mbSearcher = new ManagementObjectSearcher(
                    "SELECT Manufacturer, Product FROM Win32_BaseBoard"))
                {
                    foreach (ManagementObject mb in mbSearcher.Get())
                    {
                        string manufacturer = mb["Manufacturer"]?.ToString() ?? "Unknown";
                        string product = mb["Product"]?.ToString() ?? "Unknown";
                        hardwareInfo.Motherboard = $"{manufacturer} {product}";
                        break;
                    }
                }

                // Obtener versión de BIOS
                using (ManagementObjectSearcher biosSearcher = new ManagementObjectSearcher(
                    "SELECT Manufacturer, Version, ReleaseDate FROM Win32_BIOS"))
                {
                    foreach (ManagementObject bios in biosSearcher.Get())
                    {
                        string manufacturer = bios["Manufacturer"]?.ToString() ?? "Unknown";
                        string version = bios["Version"]?.ToString() ?? "Unknown";
                        hardwareInfo.BiosVersion = $"{manufacturer} {version}";
                        break;
                    }
                }

                // Obtener información de almacenamiento
                using (ManagementObjectSearcher diskSearcher = new ManagementObjectSearcher(
                    "SELECT Model, Size FROM Win32_DiskDrive"))
                {
                    List<string> disks = new List<string>();
                    foreach (ManagementObject disk in diskSearcher.Get())
                    {
                        if (disk["Model"] != null && disk["Size"] != null)
                        {
                            string model = disk["Model"].ToString();
                            ulong size = Convert.ToUInt64(disk["Size"]);
                            double sizeGB = size / (1024.0 * 1024 * 1024);
                            disks.Add($"{model} ({sizeGB:F1} GB)");
                        }
                    }
                    hardwareInfo.Storage = string.Join(", ", disks);
                }

                // Obtener adaptadores de red
                using (ManagementObjectSearcher netSearcher = new ManagementObjectSearcher(
                    "SELECT Description, MACAddress FROM Win32_NetworkAdapter WHERE PhysicalAdapter=True"))
                {
                    List<string> adapters = new List<string>();
                    foreach (ManagementObject net in netSearcher.Get())
                    {
                        if (net["Description"] != null)
                        {
                            string desc = net["Description"].ToString();
                            string mac = net["MACAddress"]?.ToString() ?? "N/A";
                            adapters.Add($"{desc} ({mac})");
                        }
                    }
                    hardwareInfo.NetworkAdapters = string.Join(", ", adapters);
                }

                // Generar un ID de hardware único
                hardwareInfo.HardwareId = GenerateHardwareId(hardwareInfo);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error obteniendo información de hardware: {ex.Message}");
            }

            return hardwareInfo;
        }

        private string GenerateHardwareId(HardwareInfo info)
        {
            // Crear un identificador único basado en componentes de hardware
            string baseString = $"{info.Cpu}|{info.Motherboard}|{info.BiosVersion}";
            using (var sha = System.Security.Cryptography.SHA256.Create())
            {
                byte[] bytes = System.Text.Encoding.UTF8.GetBytes(baseString);
                byte[] hash = sha.ComputeHash(bytes);
                return BitConverter.ToString(hash).Replace("-", "").Substring(0, 32);
            }
        }

        public List<NetworkConnection> GetNetworkConnections()
        {
            List<NetworkConnection> connections = new List<NetworkConnection>();

            try
            {
                // Obtener la información de todas las conexiones TCP activas
                IPGlobalProperties properties = IPGlobalProperties.GetIPGlobalProperties();

                // Conexiones TCP
                TcpConnectionInformation[] tcpConnections = properties.GetActiveTcpConnections();
                foreach (TcpConnectionInformation tcpConnection in tcpConnections)
                {
                    try
                    {
                        NetworkConnection connection = new NetworkConnection
                        {
                            LocalAddress = tcpConnection.LocalEndPoint.Address.ToString(),
                            LocalPort = tcpConnection.LocalEndPoint.Port,
                            RemoteAddress = tcpConnection.RemoteEndPoint.Address.ToString(),
                            RemotePort = tcpConnection.RemoteEndPoint.Port,
                            State = tcpConnection.State.ToString(),
                            Protocol = "TCP"
                        };

                        // Intentar obtener el proceso asociado a esta conexión
                        connection.ProcessId = GetProcessIdForTcpConnection(tcpConnection);

                        if (connection.ProcessId > 0)
                        {
                            try
                            {
                                Process process = Process.GetProcessById(connection.ProcessId);
                                connection.ProcessName = process.ProcessName;
                            }
                            catch
                            {
                                connection.ProcessName = "Unknown";
                            }
                        }

                        connections.Add(connection);
                    }
                    catch
                    {
                        // Ignorar errores en conexiones individuales
                    }
                }

                // Conexiones UDP (solo puertos escuchando)
                IPEndPoint[] udpListeners = properties.GetActiveUdpListeners();
                foreach (IPEndPoint udpListener in udpListeners)
                {
                    try
                    {
                        NetworkConnection connection = new NetworkConnection
                        {
                            LocalAddress = udpListener.Address.ToString(),
                            LocalPort = udpListener.Port,
                            RemoteAddress = "0.0.0.0",
                            RemotePort = 0,
                            State = "Listening",
                            Protocol = "UDP"
                        };

                        // Nota: no es fácil obtener el PID para listeners UDP sin P/Invoke

                        connections.Add(connection);
                    }
                    catch
                    {
                        // Ignorar errores en conexiones individuales
                    }
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error obteniendo conexiones de red: {ex.Message}");
            }

            return connections;
        }

        private int GetProcessIdForTcpConnection(TcpConnectionInformation tcpConnection)
        {
            try
            {
                // Usar netstat para obtener el PID
                using (Process process = new Process())
                {
                    ProcessStartInfo startInfo = new ProcessStartInfo
                    {
                        FileName = "netstat",
                        Arguments = "-ano",
                        UseShellExecute = false,
                        RedirectStandardOutput = true,
                        CreateNoWindow = true
                    };

                    process.StartInfo = startInfo;
                    process.Start();

                    string output = process.StandardOutput.ReadToEnd();
                    process.WaitForExit();

                    // Buscar la línea que coincide con nuestra conexión
                    string localEp = $"{tcpConnection.LocalEndPoint.Address}:{tcpConnection.LocalEndPoint.Port}";
                    string remoteEp = $"{tcpConnection.RemoteEndPoint.Address}:{tcpConnection.RemoteEndPoint.Port}";

                    string[] lines = output.Split('\n');
                    foreach (string line in lines)
                    {
                        if (line.Contains("TCP") && line.Contains(localEp.Replace("::1", "127.0.0.1")) &&
                            line.Contains(remoteEp.Replace("::1", "127.0.0.1")))
                        {
                            string[] parts = line.Trim().Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries);
                            if (parts.Length >= 5)
                            {
                                if (int.TryParse(parts[4], out int pid))
                                {
                                    return pid;
                                }
                            }
                        }
                    }
                }
            }
            catch
            {
                // Ignorar errores
            }

            return -1;
        }

        public List<DriverInfo> GetLoadedDrivers()
        {
            List<DriverInfo> drivers = new List<DriverInfo>();

            try
            {
                using (var searcher = new ManagementObjectSearcher(
                    "SELECT * FROM Win32_SystemDriver"))
                {
                    foreach (var driver in searcher.Get())
                    {
                        try
                        {
                            string name = driver["Name"]?.ToString() ?? "Unknown";
                            string displayName = driver["DisplayName"]?.ToString() ?? "";
                            string description = driver["Description"]?.ToString() ?? "";
                            string pathName = driver["PathName"]?.ToString() ?? "";
                            string startType = driver["StartMode"]?.ToString() ?? "";
                            string state = driver["State"]?.ToString() ?? "";

                            DriverInfo driverInfo = new DriverInfo
                            {
                                Name = name,
                                DisplayName = displayName,
                                Description = description,
                                PathName = pathName,
                                StartType = startType,
                                State = state,
                                // Verificar firma - simplificado
                                IsSigned = !string.IsNullOrEmpty(pathName) && IsFileSigned(pathName)
                            };

                            drivers.Add(driverInfo);
                        }
                        catch
                        {
                            // Ignorar errores en drivers individuales
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error obteniendo drivers: {ex.Message}");
            }

            return drivers;
        }

        private bool IsFileSigned(string filePath)
        {
            try
            {
                // Simplificado: en una implementación real se usaría WinVerifyTrust
                FileVersionInfo versionInfo = FileVersionInfo.GetVersionInfo(filePath);
                return !string.IsNullOrEmpty(versionInfo.CompanyName);
            }
            catch
            {
                return false;
            }
        }

        private string GetLastBootTime()
        {
            try
            {
                using (var searcher = new ManagementObjectSearcher(
                    "SELECT LastBootUpTime FROM Win32_OperatingSystem"))
                {
                    foreach (var os in searcher.Get())
                    {
                        if (os["LastBootUpTime"] != null)
                        {
                            var bootTime = ManagementDateTimeConverter.ToDateTime(os["LastBootUpTime"].ToString());
                            return bootTime.ToString("yyyy-MM-dd HH:mm:ss");
                        }
                    }
                }
            }
            catch
            {
                // Ignorar errores
            }

            return "Unknown";
        }

        public List<DeviceInfo> GetConnectedDevices()
        {
            List<DeviceInfo> devices = new List<DeviceInfo>();

            try
            {
                // Obtener todos los dispositivos USB
                using (var searcher = new ManagementObjectSearcher(
                    "SELECT * FROM Win32_PnPEntity"))
                {
                    foreach (ManagementObject device in searcher.Get())
                    {
                        try
                        {
                            string deviceId = device["DeviceID"]?.ToString() ?? "";
                            string name = device["Name"]?.ToString() ?? "Unknown Device";
                            string description = device["Description"]?.ToString() ?? "";
                            string manufacturer = device["Manufacturer"]?.ToString() ?? "";
                            string status = device["Status"]?.ToString() ?? "";
                            string deviceClass = device["ClassGuid"]?.ToString() ?? "";

                            // Crear objeto de dispositivo
                            DeviceInfo deviceInfo = new DeviceInfo
                            {
                                DeviceId = deviceId,
                                Name = name,
                                Description = description,
                                Manufacturer = manufacturer,
                                Status = status,
                                ClassGuid = deviceClass,
                                // Clasificar dispositivo según su tipo
                                TrustLevel = ClassifyDevice(deviceId, name, description, manufacturer)
                            };

                            devices.Add(deviceInfo);
                        }
                        catch
                        {
                            // Ignorar errores individuales de dispositivos
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error obteniendo dispositivos: {ex.Message}");
            }

            return devices;
        }

        private string ClassifyDevice(string deviceId, string name, string description, string manufacturer)
        {
            // Lista de palabras clave para dispositivos externos/sospechosos
            string[] externalKeywords = new string[]
            {
                "usb", "flash", "removable", "portable", "external", "card reader",
                "memory stick", "sandisk", "kingston", "cruzer"
            };

            // Comprobación para dispositivos externos
            string lowerDeviceInfo = (deviceId + " " + name + " " + description + " " + manufacturer).ToLower();

            foreach (string keyword in externalKeywords)
            {
                if (lowerDeviceInfo.Contains(keyword))
                {
                    return Core.Config.Constants.DeviceTypes.External;
                }
            }

            // Lista de fabricantes de confianza
            string[] trustedManufacturers = new string[]
            {
                "microsoft", "intel", "amd", "nvidia", "realtek", "logitech", "dell",
                "hp", "lenovo", "asus", "msi", "gigabyte", "corsair"
            };

            string lowerManufacturer = manufacturer.ToLower();

            foreach (string trusted in trustedManufacturers)
            {
                if (lowerManufacturer.Contains(trusted))
                {
                    return Core.Config.Constants.DeviceTypes.Trusted;
                }
            }

            // Si no se puede clasificar como externo o de confianza
            return Core.Config.Constants.DeviceTypes.Unknown;
        }
    }
}