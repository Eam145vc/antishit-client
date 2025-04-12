namespace AntiCheatClient.Core.Config
{
    public static class AppSettings
    {
        // URL base de la API
        public static string ApiBaseUrl { get; set; } = "https://antishit-server2-0.onrender.com/api";

        // Intervalo de monitoreo en milisegundos
        public static int MonitorIntervalMs { get; set; } = 30000;

        // Intervalo de comprobación de dispositivos
        public static int DeviceCheckIntervalMs { get; set; } = 5000;

        // Máximo tamaño de captura de pantalla (en bytes)
        public static int MaxScreenshotSize { get; set; } = 1024 * 1024 * 5; // 5MB

        // Modo de depuración - Habilitar para más logs
        public static bool DebugMode { get; set; } = true;

        // Omitir verificación de API - activar esta opción si el API no está disponible
        public static bool SkipApiVerification { get; set; } = false; // Cambiado a false para forzar conexión

        // Timeout del API en segundos
        public static int ApiTimeoutSeconds { get; set; } = 10; // Aumentado para dar más tiempo

        // Número de reintentos de conexión
        public static int ConnectionRetries { get; set; } = 3;

        // Mostrar errores detallados al usuario
        public static bool ShowDetailedErrors { get; set; } = true;

        // Habilitar WebSocket para recibir comandos en tiempo real
        public static bool EnableWebSocket { get; set; } = true;

        // Intervalo de ping de WebSocket en segundos
        public static int WebSocketPingIntervalSeconds { get; set; } = 30;
    }
}
