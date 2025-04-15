using System;
using System.Collections.Generic;
using System.Management;
using System.Windows.Forms;
using System.Runtime.InteropServices;
using System.Text;
using System.Diagnostics;
using AntiCheatClient.Core.Models;

namespace AntiCheatClient.DetectionEngine
{
    public class MonitorDetector
    {
        #region DLL Imports para acceso directo a la API de Windows

        [DllImport("user32.dll")]
        private static extern bool EnumDisplayDevices(string lpDevice, uint iDevNum, ref DISPLAY_DEVICE lpDisplayDevice, uint dwFlags);

        [DllImport("user32.dll")]
        private static extern bool EnumDisplayMonitors(IntPtr hdc, IntPtr lprcClip, MonitorEnumProc lpfnEnum, IntPtr dwData);

        [DllImport("user32.dll", CharSet = CharSet.Auto)]
        private static extern bool GetMonitorInfo(IntPtr hMonitor, ref MONITORINFOEX lpmi);

        [DllImport("user32.dll", SetLastError = true)]
        private static extern IntPtr SetupDiGetClassDevs(ref Guid ClassGuid, [MarshalAs(UnmanagedType.LPStr)] string Enumerator, IntPtr hwndParent, uint Flags);

        private delegate bool MonitorEnumProc(IntPtr hMonitor, IntPtr hdcMonitor, ref Rect lprcMonitor, IntPtr dwData);

        #endregion

        #region Estructuras de datos para la API de Windows

        [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Auto)]
        private struct DISPLAY_DEVICE
        {
            public uint cb;
            [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 32)]
            public string DeviceName;
            [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 128)]
            public string DeviceString;
            public uint StateFlags;
            [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 128)]
            public string DeviceID;
            [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 128)]
            public string DeviceKey;
        }

        [StructLayout(LayoutKind.Sequential)]
        private struct Rect
        {
            public int Left;
            public int Top;
            public int Right;
            public int Bottom;
        }

        [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Auto)]
        private struct MONITORINFOEX
        {
            public uint cbSize;
            public Rect rcMonitor;
            public Rect rcWork;
            public uint dwFlags;
            [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 32)]
            public string szDevice;
        }

        #endregion

        private List<MonitorInfo> _detectedMonitors = new List<MonitorInfo>();

        /// <summary>
        /// Detecta todos los monitores conectados al sistema y recopila información detallada
        /// </summary>
        /// <returns>Lista de información de monitores</returns>
        public List<MonitorInfo> DetectMonitors()
        {
            _detectedMonitors.Clear();

            try
            {
                // Método 1: WMI para obtener información básica
                DetectMonitorsViaWMI();

                // Método 2: EnumDisplayDevices para información adicional
                DetectMonitorsViaDisplayDevices();

                // Método 3: Screen.AllScreens para resoluciones actuales
                DetectMonitorsViaScreen();

                // Método 4: EDID para información avanzada (si está disponible)
                EnhanceMonitorsWithEDID();

                // Devolver la lista de monitores consolidada
                return _detectedMonitors;
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Error al detectar monitores: {ex.Message}");
                Console.WriteLine($"Error al detectar monitores: {ex.Message}");
                return _detectedMonitors;
            }
        }

        /// <summary>
        /// Detecta monitores usando WMI
        /// </summary>
        private void DetectMonitorsViaWMI()
        {
            try
            {
                using (var searcher = new ManagementObjectSearcher(
                    "SELECT * FROM Win32_DesktopMonitor"))
                {
                    foreach (ManagementObject monitor in searcher.Get())
                    {
                        try
                        {
                            // Crear objeto de información de monitor
                            var monitorInfo = new MonitorInfo
                            {
                                DeviceId = monitor["DeviceID"]?.ToString() ?? "Unknown-WMI",
                                Name = monitor["Name"]?.ToString() ?? "Unknown Monitor",
                                Description = monitor["Description"]?.ToString() ?? "",
                                MonitorManufacturer = monitor["MonitorManufacturer"]?.ToString() ?? "Unknown Manufacturer",
                                ScreenWidth = Convert.ToInt32(monitor["ScreenWidth"] ?? 0),
                                ScreenHeight = Convert.ToInt32(monitor["ScreenHeight"] ?? 0)
                            };

                            // Obtener tamaño físico si está disponible
                            if (monitor["MonitorType"] != null)
                            {
                                monitorInfo.MonitorType = monitor["MonitorType"].ToString();
                            }

                            // Obtener número de serie si está disponible
                            if (monitor["PNPDeviceID"] != null)
                            {
                                monitorInfo.PnpDeviceId = monitor["PNPDeviceID"].ToString();

                                // Extraer información adicional del PNP Device ID
                                if (monitorInfo.PnpDeviceId.StartsWith("DISPLAY\\"))
                                {
                                    string[] parts = monitorInfo.PnpDeviceId.Split('\\');
                                    if (parts.Length >= 3)
                                    {
                                        monitorInfo.MonitorID = parts[2];

                                        // El modelo suele estar en la segunda parte
                                        if (parts.Length >= 2)
                                        {
                                            monitorInfo.MonitorModel = parts[1];
                                        }
                                    }
                                }
                            }

                            // Añadir fuente de los datos
                            monitorInfo.DataSource = "WMI";

                            // Añadir a la lista
                            _detectedMonitors.Add(monitorInfo);
                        }
                        catch (Exception ex)
                        {
                            Debug.WriteLine($"Error procesando monitor WMI: {ex.Message}");
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Error en DetectMonitorsViaWMI: {ex.Message}");
            }
        }

        /// <summary>
        /// Detecta monitores usando EnumDisplayDevices de Windows
        /// </summary>
        private void DetectMonitorsViaDisplayDevices()
        {
            try
            {
                uint deviceIndex = 0;
                DISPLAY_DEVICE displayDevice = new DISPLAY_DEVICE();
                displayDevice.cb = (uint)Marshal.SizeOf(displayDevice);

                // Iterar sobre adaptadores de pantalla
                while (EnumDisplayDevices(null, deviceIndex, ref displayDevice, 0))
                {
                    if ((displayDevice.StateFlags & 0x1) != 0) // Dispositivo activo
                    {
                        uint monitorIndex = 0;
                        DISPLAY_DEVICE monitorDevice = new DISPLAY_DEVICE();
                        monitorDevice.cb = (uint)Marshal.SizeOf(monitorDevice);

                        // Iterar sobre monitores conectados a este adaptador
                        while (EnumDisplayDevices(displayDevice.DeviceName, monitorIndex, ref monitorDevice, 0))
                        {
                            try
                            {
                                // Crear o buscar info de monitor existente
                                MonitorInfo monitorInfo = FindOrCreateMonitor(monitorDevice.DeviceID);

                                // Añadir información del dispositivo
                                monitorInfo.DeviceName = monitorDevice.DeviceName;
                                monitorInfo.Description = monitorDevice.DeviceString;

                                // Extraer información del ID de dispositivo si está en formato estándar
                                if (monitorDevice.DeviceID.Contains("MONITOR\\"))
                                {
                                    string[] parts = monitorDevice.DeviceID.Split('\\');
                                    if (parts.Length >= 2)
                                    {
                                        string[] vendorProduct = parts[1].Split('&');
                                        if (vendorProduct.Length >= 2)
                                        {
                                            monitorInfo.MonitorManufacturer = DecodeEDIDManufacturerCode(vendorProduct[0]);
                                            monitorInfo.MonitorModel = vendorProduct[1];
                                        }
                                    }
                                }

                                // Añadir fuente si es nueva
                                if (!monitorInfo.DataSource.Contains("DisplayDevice"))
                                {
                                    monitorInfo.DataSource += (string.IsNullOrEmpty(monitorInfo.DataSource) ? "" : ", ") + "DisplayDevice";
                                }

                                // Información de la tarjeta gráfica
                                monitorInfo.GraphicsCardName = displayDevice.DeviceString;
                            }
                            catch (Exception ex)
                            {
                                Debug.WriteLine($"Error procesando monitor EnumDisplay: {ex.Message}");
                            }

                            // Siguiente monitor
                            monitorIndex++;
                            monitorDevice.cb = (uint)Marshal.SizeOf(monitorDevice);
                        }
                    }

                    // Siguiente adaptador
                    deviceIndex++;
                    displayDevice.cb = (uint)Marshal.SizeOf(displayDevice);
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Error en DetectMonitorsViaDisplayDevices: {ex.Message}");
            }
        }

        /// <summary>
        /// Detecta resoluciones y configuración actual usando Screen.AllScreens
        /// </summary>
        private void DetectMonitorsViaScreen()
        {
            try
            {
                Screen[] screens = Screen.AllScreens;
                for (int i = 0; i < screens.Length; i++)
                {
                    Screen screen = screens[i];

                    // Crear nueva entrada o actualizar existente
                    MonitorInfo monitorInfo;

                    // Para los monitores detectados por Screen, usamos un ID basado en índice
                    string tempId = $"SCREEN_{i}";

                    // Buscar si ya existe un monitor con datos similares
                    if (i < _detectedMonitors.Count)
                    {
                        monitorInfo = _detectedMonitors[i];
                    }
                    else
                    {
                        monitorInfo = new MonitorInfo
                        {
                            DeviceId = tempId,
                            Name = $"Display {i + 1}"
                        };
                        _detectedMonitors.Add(monitorInfo);
                    }

                    // Actualizar información de resolución
                    monitorInfo.ScreenWidth = screen.Bounds.Width;
                    monitorInfo.ScreenHeight = screen.Bounds.Height;
                    monitorInfo.WorkingAreaWidth = screen.WorkingArea.Width;
                    monitorInfo.WorkingAreaHeight = screen.WorkingArea.Height;
                    monitorInfo.BitsPerPixel = Screen.PrimaryScreen.BitsPerPixel;
                    monitorInfo.IsPrimary = screen.Primary;

                    // Añadir fuente si es nueva
                    if (!monitorInfo.DataSource.Contains("Screen"))
                    {
                        monitorInfo.DataSource += (string.IsNullOrEmpty(monitorInfo.DataSource) ? "" : ", ") + "Screen";
                    }
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Error en DetectMonitorsViaScreen: {ex.Message}");
            }
        }

        /// <summary>
        /// Mejora la información de monitores con datos de EDID (Extended Display Identification Data)
        /// </summary>
        private void EnhanceMonitorsWithEDID()
        {
            try
            {
                using (var searcher = new ManagementObjectSearcher(
                    "SELECT * FROM WmiMonitorID"))
                {
                    foreach (ManagementObject monitor in searcher.Get())
                    {
                        try
                        {
                            // Obtener InstanceName que contiene el ID de dispositivo PnP
                            string instanceName = monitor["InstanceName"] as string;
                            if (string.IsNullOrEmpty(instanceName))
                                continue;

                            // Buscar el monitor correspondiente
                            MonitorInfo matchingMonitor = null;
                            foreach (var monitorInfo in _detectedMonitors)
                            {
                                if (!string.IsNullOrEmpty(monitorInfo.PnpDeviceId) &&
                                    instanceName.Contains(monitorInfo.PnpDeviceId.Split('\\').Last()))
                                {
                                    matchingMonitor = monitorInfo;
                                    break;
                                }
                            }

                            // Si no se encontró, intentar por DeviceID
                            if (matchingMonitor == null)
                            {
                                foreach (var monitorInfo in _detectedMonitors)
                                {
                                    if (!string.IsNullOrEmpty(monitorInfo.DeviceId) &&
                                        instanceName.Contains(monitorInfo.DeviceId.Split('\\').Last()))
                                    {
                                        matchingMonitor = monitorInfo;
                                        break;
                                    }
                                }
                            }

                            // Si todavía no se encontró, usar el primero disponible
                            if (matchingMonitor == null && _detectedMonitors.Count > 0)
                            {
                                matchingMonitor = _detectedMonitors[0];
                            }

                            if (matchingMonitor != null)
                            {
                                // Decodificar fabricante
                                if (monitor["ManufacturerName"] != null)
                                {
                                    byte[] manufacturerNameBytes = monitor["ManufacturerName"] as byte[];
                                    if (manufacturerNameBytes != null && manufacturerNameBytes.Length > 0)
                                    {
                                        matchingMonitor.MonitorManufacturer = ConvertBytesToString(manufacturerNameBytes);
                                    }
                                }

                                // Decodificar nombre del producto
                                if (monitor["ProductCodeID"] != null)
                                {
                                    byte[] productCodeBytes = monitor["ProductCodeID"] as byte[];
                                    if (productCodeBytes != null && productCodeBytes.Length > 0)
                                    {
                                        matchingMonitor.MonitorModel = ConvertBytesToString(productCodeBytes);
                                    }
                                }

                                // Decodificar número de serie
                                if (monitor["SerialNumberID"] != null)
                                {
                                    byte[] serialNumberBytes = monitor["SerialNumberID"] as byte[];
                                    if (serialNumberBytes != null && serialNumberBytes.Length > 0)
                                    {
                                        matchingMonitor.SerialNumber = ConvertBytesToString(serialNumberBytes);
                                    }
                                }

                                // Año de fabricación
                                if (monitor["YearOfManufacture"] != null)
                                {
                                    matchingMonitor.YearOfManufacture = Convert.ToInt32(monitor["YearOfManufacture"]);
                                }

                                // Semana de fabricación
                                if (monitor["WeekOfManufacture"] != null)
                                {
                                    matchingMonitor.WeekOfManufacture = Convert.ToInt32(monitor["WeekOfManufacture"]);
                                }

                                // Añadir fuente EDID
                                if (!matchingMonitor.DataSource.Contains("EDID"))
                                {
                                    matchingMonitor.DataSource += (string.IsNullOrEmpty(matchingMonitor.DataSource) ? "" : ", ") + "EDID";
                                }
                            }
                        }
                        catch (Exception ex)
                        {
                            Debug.WriteLine($"Error procesando EDID: {ex.Message}");
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Error en EnhanceMonitorsWithEDID: {ex.Message}");
            }
        }

        /// <summary>
        /// Busca un monitor existente o crea uno nuevo
        /// </summary>
        private MonitorInfo FindOrCreateMonitor(string deviceId)
        {
            foreach (var monitor in _detectedMonitors)
            {
                if (monitor.DeviceId == deviceId)
                {
                    return monitor;
                }
            }

            // Si no se encuentra, crear uno nuevo
            var newMonitor = new MonitorInfo
            {
                DeviceId = deviceId
            };
            _detectedMonitors.Add(newMonitor);
            return newMonitor;
        }

        /// <summary>
        /// Decodifica el código de fabricante EDID
        /// </summary>
        private string DecodeEDIDManufacturerCode(string code)
        {
            try
            {
                if (string.IsNullOrEmpty(code) || code.Length < 3)
                {
                    return "Unknown";
                }

                // Los códigos EDID utilizan 3 caracteres
                switch (code.ToUpper())
                {
                    case "ACI": return "Ancor Communications Inc";
                    case "ACR": return "Acer";
                    case "AUO": return "AU Optronics";
                    case "APP": return "Apple";
                    case "BNQ": return "BenQ";
                    case "CMI": return "Chimei Innolux";
                    case "DEL": return "Dell";
                    case "HPN": return "HP";
                    case "HWP": return "HP";
                    case "LEN": return "Lenovo";
                    case "LGD": return "LG Display";
                    case "LPL": return "LG Philips";
                    case "NEC": return "NEC";
                    case "SAM": return "Samsung";
                    case "SEC": return "Seiko Epson";
                    case "SHP": return "Sharp";
                    case "SNY": return "Sony";
                    case "VSC": return "ViewSonic";
                    default: return code;
                }
            }
            catch
            {
                return code;
            }
        }

        /// <summary>
        /// Convierte un array de bytes a string para datos EDID
        /// </summary>
        private string ConvertBytesToString(byte[] bytes)
        {
            try
            {
                StringBuilder builder = new StringBuilder(bytes.Length);
                foreach (byte b in bytes)
                {
                    if (b != 0) // Ignorar bytes nulos
                    {
                        builder.Append((char)b);
                    }
                }
                return builder.ToString().Trim();
            }
            catch
            {
                return "Unknown";
            }
        }
    }
}