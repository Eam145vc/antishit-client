using System;
using System.Text.Json.Serialization;
using Newtonsoft.Json;

namespace AntiCheatClient.Core.Models
{
    /// <summary>
    /// Clase que almacena información detallada sobre un monitor
    /// </summary>
    public class MonitorInfo
    {
        // Identificadores de dispositivo
        public string DeviceId { get; set; } = "Unknown";
        public string PnpDeviceId { get; set; } = "";
        public string DeviceName { get; set; } = "";
        public string MonitorID { get; set; } = "";

        // Información básica
        public string Name { get; set; } = "Unknown Monitor";
        public string Description { get; set; } = "";
        public string MonitorType { get; set; } = "";

        // Fabricante y modelo
        public string MonitorManufacturer { get; set; } = "Unknown";
        public string MonitorModel { get; set; } = "";
        public string SerialNumber { get; set; } = "";

        // Año y semana de fabricación
        public int YearOfManufacture { get; set; }
        public int WeekOfManufacture { get; set; }

        // Información de pantalla
        public int ScreenWidth { get; set; }
        public int ScreenHeight { get; set; }
        public int WorkingAreaWidth { get; set; }
        public int WorkingAreaHeight { get; set; }
        public int BitsPerPixel { get; set; }
        public bool IsPrimary { get; set; }

        // Estado de conexión
        public string ConnectionStatus { get; set; } = "Connected";

        // Información de la tarjeta gráfica
        public string GraphicsCardName { get; set; } = "";

        // Metadatos
        [JsonIgnore]
        public string DataSource { get; set; } = "";

        /// <summary>
        /// Obtiene una descripción formateada de la resolución
        /// </summary>
        public string GetResolutionString()
        {
            if (ScreenWidth > 0 && ScreenHeight > 0)
            {
                return $"{ScreenWidth}x{ScreenHeight}";
            }
            return "Unknown Resolution";
        }

        /// <summary>
        /// Obtiene una descripción amigable del monitor
        /// </summary>
        public string GetFriendlyName()
        {
            string brand = !string.IsNullOrEmpty(MonitorManufacturer) && MonitorManufacturer != "Unknown"
                ? MonitorManufacturer
                : "";

            string model = !string.IsNullOrEmpty(MonitorModel)
                ? MonitorModel
                : "";

            string resolution = ScreenWidth > 0 && ScreenHeight > 0
                ? $" ({ScreenWidth}x{ScreenHeight})"
                : "";

            if (!string.IsNullOrEmpty(brand) || !string.IsNullOrEmpty(model))
            {
                return $"{brand} {model}{resolution}".Trim();
            }

            return !string.IsNullOrEmpty(Name) && Name != "Unknown Monitor"
                ? $"{Name}{resolution}"
                : $"Monitor {resolution}";
        }

        /// <summary>
        /// Convierte la información del monitor a un objeto DeviceInfo para enviar al servidor
        /// </summary>
        public DeviceInfo ToDeviceInfo()
        {
            return new DeviceInfo
            {
                DeviceId = this.DeviceId,
                Name = this.GetFriendlyName(),
                Description = $"{this.GetResolutionString()} - {this.Description}".Trim(),
                Manufacturer = this.MonitorManufacturer,
                Type = "Monitor",
                Status = "OK",
                ConnectionStatus = this.ConnectionStatus,
                DeviceClass = "Display",
                ClassGuid = "{4D36E96E-E325-11CE-BFC1-08002BE10318}", // GUID de clase para monitores
                Driver = this.GraphicsCardName,
                HardwareId = this.PnpDeviceId,
                TrustLevel = "Trusted" // Por lo general los monitores son dispositivos confiables
            };
        }

        /// <summary>
        /// Información del monitor en formato JSON para enviar al servidor
        /// </summary>
        public object GetMonitorInfo()
        {
            return new
            {
                deviceId = this.DeviceId,
                manufacturer = this.MonitorManufacturer,
                model = this.MonitorModel,
                serialNumber = this.SerialNumber,
                resolution = this.GetResolutionString(),
                yearOfManufacture = this.YearOfManufacture > 0 ? this.YearOfManufacture : null,
                isPrimary = this.IsPrimary,
                bitsPerPixel = this.BitsPerPixel > 0 ? this.BitsPerPixel : null,
                graphicsCard = this.GraphicsCardName
            };
        }
    }
}