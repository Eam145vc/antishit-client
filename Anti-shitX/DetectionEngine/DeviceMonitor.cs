using System;
using System.Collections.Generic;
using System.Management;
using System.Threading;
using System.Threading.Tasks;
using AntiCheatClient.Core.Config;
using AntiCheatClient.Core.Models;

namespace AntiCheatClient.DetectionEngine
{
    public class DeviceMonitor
    {
        private List<DeviceInfo> _currentDevices = new List<DeviceInfo>();
        private ManagementEventWatcher _insertWatcher;
        private ManagementEventWatcher _removeWatcher;
        private Timer _deviceCheckTimer;

        public event EventHandler<DeviceChangedEventArgs> DeviceChanged;

        public void Initialize()
        {
            // Obtener lista inicial de dispositivos
            _currentDevices = GetConnectedDevices();

            // Configurar WMI para eventos de dispositivos
            SetupDeviceWatchers();

            // Configurar timer para verificación periódica (como respaldo)
            _deviceCheckTimer = new Timer(CheckDevicesTimerCallback, null,
                AppSettings.DeviceCheckIntervalMs, AppSettings.DeviceCheckIntervalMs);
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

                // Obtener monitores conectados específicamente
                using (var searcher = new ManagementObjectSearcher(
                    "SELECT * FROM Win32_DesktopMonitor"))
                {
                    foreach (ManagementObject monitor in searcher.Get())
                    {
                        try
                        {
                            string deviceId = monitor["DeviceID"]?.ToString() ?? "";
                            string name = monitor["Name"]?.ToString() ?? "Unknown Monitor";
                            string description = monitor["Description"]?.ToString() ?? "";
                            string status = monitor["Status"]?.ToString() ?? "";

                            DeviceInfo deviceInfo = new DeviceInfo
                            {
                                DeviceId = deviceId,
                                Name = name,
                                Description = description,
                                Status = status,
                                Type = "Monitor",
                                TrustLevel = Constants.DeviceTypes.Trusted
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
                    return Constants.DeviceTypes.External;
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
                    return Constants.DeviceTypes.Trusted;
                }
            }

            // Si no se puede clasificar como externo o de confianza
            return Constants.DeviceTypes.Unknown;
        }

        private void SetupDeviceWatchers()
        {
            try
            {
                // Configurar watcher para inserción de dispositivos
                _insertWatcher = new ManagementEventWatcher();
                var insertQuery = new WqlEventQuery(
                    "SELECT * FROM __InstanceCreationEvent WITHIN 2 WHERE TargetInstance ISA 'Win32_PnPEntity'");
                _insertWatcher.Query = insertQuery;
                _insertWatcher.EventArrived += DeviceInsertedEvent;
                _insertWatcher.Start();

                // Configurar watcher para remoción de dispositivos
                _removeWatcher = new ManagementEventWatcher();
                var removeQuery = new WqlEventQuery(
                    "SELECT * FROM __InstanceDeletionEvent WITHIN 2 WHERE TargetInstance ISA 'Win32_PnPEntity'");
                _removeWatcher.Query = removeQuery;
                _removeWatcher.EventArrived += DeviceRemovedEvent;
                _removeWatcher.Start();
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error configurando watchers de dispositivos: {ex.Message}");
            }
        }

        private void DeviceInsertedEvent(object sender, EventArrivedEventArgs e)
        {
            try
            {
                ManagementBaseObject instance = (ManagementBaseObject)e.NewEvent["TargetInstance"];

                string deviceId = instance["DeviceID"]?.ToString() ?? "";
                string name = instance["Name"]?.ToString() ?? "Unknown Device";
                string description = instance["Description"]?.ToString() ?? "";
                string manufacturer = instance["Manufacturer"]?.ToString() ?? "";
                string status = instance["Status"]?.ToString() ?? "";

                DeviceInfo deviceInfo = new DeviceInfo
                {
                    DeviceId = deviceId,
                    Name = name,
                    Description = description,
                    Manufacturer = manufacturer,
                    Status = status,
                    TrustLevel = ClassifyDevice(deviceId, name, description, manufacturer)
                };

                // Evitar duplicados
                if (!_currentDevices.Exists(d => d.DeviceId == deviceId))
                {
                    _currentDevices.Add(deviceInfo);
                    OnDeviceChanged(deviceInfo, true);
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error en evento de inserción de dispositivo: {ex.Message}");
            }
        }

        private void DeviceRemovedEvent(object sender, EventArrivedEventArgs e)
        {
            try
            {
                ManagementBaseObject instance = (ManagementBaseObject)e.NewEvent["TargetInstance"];

                string deviceId = instance["DeviceID"]?.ToString() ?? "";
                string name = instance["Name"]?.ToString() ?? "Unknown Device";

                // Buscar el dispositivo en la lista actual
                DeviceInfo deviceInfo = _currentDevices.Find(d => d.DeviceId == deviceId);

                if (deviceInfo != null)
                {
                    _currentDevices.Remove(deviceInfo);
                    OnDeviceChanged(deviceInfo, false);
                }
                else
                {
                    // Si no lo encontramos en nuestra lista, crear uno nuevo
                    deviceInfo = new DeviceInfo
                    {
                        DeviceId = deviceId,
                        Name = name,
                    };

                    OnDeviceChanged(deviceInfo, false);
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error en evento de remoción de dispositivo: {ex.Message}");
            }
        }

        private void CheckDevicesTimerCallback(object state)
        {
            Task.Run(() =>
            {
                try
                {
                    // Obtener la lista actual de dispositivos
                    List<DeviceInfo> currentDevices = GetConnectedDevices();

                    // Buscar dispositivos nuevos
                    foreach (DeviceInfo newDevice in currentDevices)
                    {
                        if (!_currentDevices.Exists(d => d.DeviceId == newDevice.DeviceId))
                        {
                            _currentDevices.Add(newDevice);
                            OnDeviceChanged(newDevice, true);
                        }
                    }

                    // Buscar dispositivos desconectados
                    List<DeviceInfo> removedDevices = new List<DeviceInfo>();

                    foreach (DeviceInfo oldDevice in _currentDevices)
                    {
                        if (!currentDevices.Exists(d => d.DeviceId == oldDevice.DeviceId))
                        {
                            removedDevices.Add(oldDevice);
                        }
                    }

                    // Procesar dispositivos desconectados
                    foreach (DeviceInfo removedDevice in removedDevices)
                    {
                        _currentDevices.Remove(removedDevice);
                        OnDeviceChanged(removedDevice, false);
                    }
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"Error en verificación periódica de dispositivos: {ex.Message}");
                }
            });
        }

        private void OnDeviceChanged(DeviceInfo deviceInfo, bool isConnected)
        {
            DeviceChanged?.Invoke(this, new DeviceChangedEventArgs
            {
                Device = deviceInfo,
                IsConnected = isConnected
            });
        }

        public void Dispose()
        {
            try
            {
                _insertWatcher?.Stop();
                _removeWatcher?.Stop();
                _deviceCheckTimer?.Dispose();
            }
            catch
            {
                // Ignorar errores al limpiar recursos
            }
        }
    }
}