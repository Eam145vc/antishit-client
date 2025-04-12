using System;
using System.Net.WebSockets;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using AntiCheatClient.Core.Config;
using System.Diagnostics;

namespace AntiCheatClient.Core.Services
{
    public class WebSocketService
    {
        private ClientWebSocket _webSocket;
        private readonly Uri _serverUri;
        private CancellationTokenSource _cancellationSource;
        private bool _isConnected = false;
        private bool _isConnecting = false;

        // Eventos que pueden ser escuchados por otros componentes
        public event EventHandler<bool> ConnectionStatusChanged;
        public event EventHandler<string> MessageReceived;
        public event EventHandler<ScreenshotRequestEventArgs> ScreenshotRequested;

        public bool IsConnected => _isConnected;

        public WebSocketService()
        {
            // Configurar URL del WebSocket quitando "api" del final si existe
            string wsUrl = AppSettings.ApiBaseUrl;

            // Si la URL tiene "api" al final, eliminarlo
            if (wsUrl.EndsWith("/api"))
            {
                wsUrl = wsUrl.Substring(0, wsUrl.Length - 4);
            }

            // Reemplazar http con ws
            wsUrl = wsUrl.Replace("http://", "ws://").Replace("https://", "wss://");

            // Asegurar que la URL termine con /
            if (!wsUrl.EndsWith("/"))
            {
                wsUrl += "/";
            }

            // Completar la URL con socket.io
            wsUrl += "socket.io/?EIO=3&transport=websocket";

            _serverUri = new Uri(wsUrl);
            Debug.WriteLine($"WebSocketService inicializado con URL: {_serverUri}");
        }

        public async Task Connect()
        {
            if (_isConnected || _isConnecting)
                return;

            _isConnecting = true;

            try
            {
                Debug.WriteLine("Iniciando conexión WebSocket...");
                _webSocket = new ClientWebSocket();
                _cancellationSource = new CancellationTokenSource();

                // Agregar token de autenticación si es necesario
                // _webSocket.Options.SetRequestHeader("Authorization", "Bearer " + token);

                // Intentar conectar al servidor
                await _webSocket.ConnectAsync(_serverUri, _cancellationSource.Token);

                _isConnected = true;
                _isConnecting = false;

                Debug.WriteLine("Conexión WebSocket establecida correctamente");
                ConnectionStatusChanged?.Invoke(this, true);

                // Iniciar tarea para recibir mensajes
                StartListening();
            }
            catch (Exception ex)
            {
                _isConnected = false;
                _isConnecting = false;
                Debug.WriteLine($"Error conectando WebSocket: {ex.Message}");
                ConnectionStatusChanged?.Invoke(this, false);
            }
        }

        private void StartListening()
        {
            Task.Run(async () =>
            {
                try
                {
                    var buffer = new byte[4096];

                    while (_webSocket.State == WebSocketState.Open && !_cancellationSource.Token.IsCancellationRequested)
                    {
                        WebSocketReceiveResult result = null;
                        var message = new StringBuilder();

                        do
                        {
                            var segment = new ArraySegment<byte>(buffer);
                            result = await _webSocket.ReceiveAsync(segment, _cancellationSource.Token);

                            if (result.MessageType == WebSocketMessageType.Close)
                            {
                                await _webSocket.CloseAsync(WebSocketCloseStatus.NormalClosure, "Closing", CancellationToken.None);
                                _isConnected = false;
                                ConnectionStatusChanged?.Invoke(this, false);
                                return;
                            }

                            var messageBytes = new byte[result.Count];
                            Array.Copy(buffer, messageBytes, result.Count);
                            message.Append(Encoding.UTF8.GetString(messageBytes));
                        }
                        while (!result.EndOfMessage);

                        // Procesar el mensaje recibido
                        ProcessReceivedMessage(message.ToString());
                    }
                }
                catch (Exception ex)
                {
                    Debug.WriteLine($"Error en recepción de WebSocket: {ex.Message}");
                    _isConnected = false;
                    ConnectionStatusChanged?.Invoke(this, false);
                }
            });
        }

        private void ProcessReceivedMessage(string message)
        {
            try
            {
                // Los mensajes de Socket.io tienen un formato específico
                Debug.WriteLine($"Mensaje WebSocket recibido: {message}");

                // Notificar a los suscriptores que se recibió un mensaje
                MessageReceived?.Invoke(this, message);

                // Socket.io usa prefijos numéricos - necesitamos analizarlos
                if (message.StartsWith("42"))
                {
                    // 42 indica un evento de socket.io
                    string eventData = message.Substring(2);
                    JArray parsedEvent = JArray.Parse(eventData);

                    // El primer elemento es el nombre del evento
                    string eventName = parsedEvent[0].ToString();
                    Debug.WriteLine($"Evento Socket.io recibido: {eventName}");

                    // El segundo elemento son los datos del evento
                    JToken eventArgs = parsedEvent[1];

                    switch (eventName)
                    {
                        case "take-screenshot":
                            HandleTakeScreenshotEvent(eventArgs);
                            break;
                            // Otros eventos pueden agregarse aquí
                    }
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Error procesando mensaje de WebSocket: {ex.Message}");
            }
        }

        private void HandleTakeScreenshotEvent(JToken eventData)
        {
            try
            {
                Debug.WriteLine("Procesando evento de solicitud de captura");

                // Extraer información relevante del evento
                string activisionId = eventData["activisionId"]?.ToString();
                string requestedBy = eventData["requestedBy"]?.ToString();

                // Verificar si estamos recibiendo la solicitud destinada a este cliente
                if (string.IsNullOrEmpty(activisionId) ||
                    activisionId != UI.Windows.MainOverlay.CurrentActivisionId)
                {
                    Debug.WriteLine($"Solicitud no corresponde a este cliente ({activisionId} vs {UI.Windows.MainOverlay.CurrentActivisionId})");
                    return;
                }

                Debug.WriteLine($"Solicitud de captura recibida desde: {requestedBy}");

                // Notificar a los suscriptores sobre la solicitud de captura
                ScreenshotRequested?.Invoke(this, new ScreenshotRequestEventArgs
                {
                    ActivisionId = activisionId,
                    RequestedBy = requestedBy
                });
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Error procesando evento take-screenshot: {ex.Message}");
            }
        }

        public async Task Disconnect()
        {
            try
            {
                if (_webSocket != null && _webSocket.State == WebSocketState.Open)
                {
                    await _webSocket.CloseAsync(WebSocketCloseStatus.NormalClosure, "Closing", CancellationToken.None);
                    Debug.WriteLine("Conexión WebSocket cerrada correctamente");
                }

                _cancellationSource?.Cancel();
                _isConnected = false;
                ConnectionStatusChanged?.Invoke(this, false);
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Error desconectando WebSocket: {ex.Message}");
            }
        }

        // Método para enviar mensajes a través del socket (si se necesita)
        public async Task SendMessage(string eventName, object data)
        {
            try
            {
                if (_webSocket == null || _webSocket.State != WebSocketState.Open)
                {
                    Debug.WriteLine("No se puede enviar mensaje: WebSocket no está conectado");
                    return;
                }

                // Crear array para el formato de socket.io: [eventName, data]
                object[] eventArray = new object[] { eventName, data };
                string message = "42" + JsonConvert.SerializeObject(eventArray);

                var bytes = Encoding.UTF8.GetBytes(message);
                var buffer = new ArraySegment<byte>(bytes);

                await _webSocket.SendAsync(buffer, WebSocketMessageType.Text, true, _cancellationSource.Token);
                Debug.WriteLine($"Mensaje enviado a través de WebSocket: {message}");
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Error enviando mensaje WebSocket: {ex.Message}");
            }
        }
    }

    // Clase para los eventos de solicitud de captura
    public class ScreenshotRequestEventArgs : EventArgs
    {
        public string ActivisionId { get; set; }
        public string RequestedBy { get; set; }
    }
}