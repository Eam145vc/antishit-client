namespace AntiCheatClient.Core.Config
{
    public static class Constants
    {
        // Mensajes
        public static class Messages
        {
            public const string CloseWarning = "ADVERTENCIA: Cerrar el monitor durante un torneo puede resultar en tu descalificación. ¿Estás seguro de que deseas continuar?";
            public const string ServerConnectionError = "Error al conectar con el servidor. Por favor, verifica tu conexión a internet.";
            public const string DeviceDetected = "Se ha detectado un nuevo dispositivo conectado. Esta información será reportada al juez del torneo.";
            public const string DeviceRemoved = "Se ha detectado la desconexión de un dispositivo. Esta información será reportada al juez del torneo.";
        }

        // Tipos de dispositivos
        public static class DeviceTypes
        {
            public const string Trusted = "Trusted";
            public const string Unknown = "Unknown";
            public const string External = "External";
        }

        // Estados de conexión
        public static class ConnectionState
        {
            public const string Connected = "Connected";
            public const string Disconnected = "Disconnected";
            public const string Connecting = "Connecting";
        }
    }
}