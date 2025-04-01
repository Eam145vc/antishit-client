namespace AntiCheatClient.Core.Config
{
    public static class AppSettings
    {
        // URL base de la API
        public static string ApiBaseUrl { get; set; } = "https://anti5-0.onrender.com/api";

        // Intervalo de monitoreo en milisegundos
        public static int MonitorIntervalMs { get; set; } = 30000;

        // Intervalo de comprobación de dispositivos
        public static int DeviceCheckIntervalMs { get; set; } = 5000;

        // Máximo tamaño de captura de pantalla (en bytes)
        public static int MaxScreenshotSize { get; set; } = 1024 * 1024 * 5; // 5MB
    }
}