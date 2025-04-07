using System;
using System.Net.Http;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Newtonsoft.Json;
using AntiCheatClient.Core.Models;
using AntiCheatClient.Core.Config;
using System.Diagnostics;
using System.Windows;
using System.Linq;

namespace AntiCheatClient.Core.Services
{
    public class ApiService
    {
        private readonly HttpClient _httpClient;
        private readonly string _baseUrl;
        private bool _isConnected;
        private string _lastErrorMessage = "";

        public bool IsConnected => _isConnected;
        public string LastErrorMessage => _lastErrorMessage;
        public event EventHandler<bool> ConnectionStatusChanged;

        public ApiService(string baseUrl)
        {
            _baseUrl = baseUrl;

            var handler = new HttpClientHandler
            {
                // Para depuración, aceptar cualquier certificado
                ServerCertificateCustomValidationCallback = (message, cert, chain, errors) => true
            };

            _httpClient = new HttpClient(handler);
            _httpClient.Timeout = TimeSpan.FromSeconds(AppSettings.ApiTimeoutSeconds);

            Debug.WriteLine($"ApiService inicializado con URL: {_baseUrl}");
            Console.WriteLine($"ApiService inicializado con URL: {_baseUrl}");

            // No asumimos que estamos conectados hasta probarlo
            _isConnected = false;
        }

        public async Task<bool> CheckConnection()
        {
            int attempts = 0;
            int maxAttempts = AppSettings.ConnectionRetries;

            while (attempts < maxAttempts)
            {
                attempts++;
                try
                {
                    _lastErrorMessage = "";
                    string endpoint = $"https://antishit-server2-0.onrender.com/";

                    Debug.WriteLine($"Verificando conexión a {endpoint} (intento {attempts}/{maxAttempts})");
                    Console.WriteLine($"Verificando conexión a {endpoint} (intento {attempts}/{maxAttempts})");

                    // Crear un mensaje personalizado para obtener más control
                    var request = new HttpRequestMessage(HttpMethod.Get, endpoint);

                    // Añadir headers para depuración
                    request.Headers.Add("User-Agent", "AntiCheatClient/1.0");
                    request.Headers.Add("Accept", "application/json");

                    var cancellationToken = new CancellationTokenSource(TimeSpan.FromSeconds(AppSettings.ApiTimeoutSeconds)).Token;

                    // Ejecutar la solicitud
                    var response = await _httpClient.SendAsync(request, cancellationToken);

                    // Capturar todos los detalles relevantes
                    string statusDetail = $"Respuesta HTTP: {(int)response.StatusCode} {response.ReasonPhrase}";
                    Debug.WriteLine(statusDetail);
                    Console.WriteLine(statusDetail);

                    // Capturar headers de respuesta para depuración
                    if (AppSettings.DebugMode)
                    {
                        Debug.WriteLine("Headers de respuesta:");
                        foreach (var header in response.Headers)
                        {
                            Debug.WriteLine($"  {header.Key}: {string.Join(", ", header.Value)}");
                        }
                    }

                    string responseContent = await response.Content.ReadAsStringAsync();
                    bool newStatus = response.IsSuccessStatusCode;

                    if (!newStatus)
                    {
                        _lastErrorMessage = $"Error HTTP {(int)response.StatusCode}: {response.ReasonPhrase}";

                        // Si no es el último intento, esperar antes de reintentar
                        if (attempts < maxAttempts)
                        {
                            Debug.WriteLine($"Esperando 1 segundo antes del reintento {attempts + 1}...");
                            await Task.Delay(1000);
                            continue;
                        }

                        Debug.WriteLine($"Error de conexión: {_lastErrorMessage}");
                        Debug.WriteLine($"Contenido de respuesta: {responseContent}");

                        // Mostrar popup detallado
                        if (AppSettings.ShowDetailedErrors)
                        {
                            await Task.Run(() => {
                                MessageBox.Show(
                                    $"Error de conexión al servidor: {_lastErrorMessage}\n\nURL: {endpoint}\n\nRespuesta: {responseContent}",
                                    "Error de Conexión",
                                    MessageBoxButton.OK,
                                    MessageBoxImage.Error
                                );
                            });
                        }
                    }
                    else
                    {
                        Debug.WriteLine($"Conexión exitosa. Contenido: {responseContent}");
                        Console.WriteLine("Conexión exitosa al servidor");
                    }

                    // Notificar cambio de estado solo si hay un cambio real
                    if (newStatus != _isConnected)
                    {
                        _isConnected = newStatus;
                        ConnectionStatusChanged?.Invoke(this, _isConnected);

                        Debug.WriteLine($"Estado de conexión cambiado a: {(_isConnected ? "CONECTADO" : "DESCONECTADO")}");
                        Console.WriteLine($"Estado de conexión cambiado a: {(_isConnected ? "CONECTADO" : "DESCONECTADO")}");
                    }

                    return _isConnected;
                }
                catch (TaskCanceledException)
                {
                    _lastErrorMessage = $"Tiempo de espera agotado ({AppSettings.ApiTimeoutSeconds} segundos) al conectar con el servidor";
                    Debug.WriteLine(_lastErrorMessage);
                    Console.WriteLine(_lastErrorMessage);

                    // Si no es el último intento, esperar antes de reintentar
                    if (attempts < maxAttempts)
                    {
                        Debug.WriteLine($"Esperando 1 segundo antes del reintento {attempts + 1}...");
                        await Task.Delay(1000);
                        continue;
                    }
                }
                catch (HttpRequestException ex)
                {
                    // Capturar errores específicos de red
                    string errorDetail = "";

                    if (ex.Message.Contains("NameResolutionFailure") || ex.Message.Contains("No such host"))
                    {
                        errorDetail = "Error de resolución DNS. No se pudo encontrar el servidor.";
                    }
                    else if (ex.Message.Contains("ConnectFailure") || ex.Message.Contains("connection attempt failed"))
                    {
                        errorDetail = "Error de conexión. Posiblemente el servidor está caído o hay un problema de red.";
                    }
                    else if (ex.Message.Contains("ssl") || ex.Message.Contains("SSL") || ex.Message.Contains("TLS"))
                    {
                        errorDetail = "Error de SSL/TLS. Hay un problema con la seguridad de la conexión.";
                    }
                    else
                    {
                        errorDetail = ex.Message;
                    }

                    _lastErrorMessage = $"Error de red: {errorDetail}";
                    Debug.WriteLine($"Error tipo: {ex.GetType().Name}");
                    Debug.WriteLine($"Error al conectar: {ex.Message}");

                    // Si no es el último intento, esperar antes de reintentar
                    if (attempts < maxAttempts)
                    {
                        Debug.WriteLine($"Esperando 1 segundo antes del reintento {attempts + 1}...");
                        await Task.Delay(1000);
                        continue;
                    }

                    // Mostrar detalles de error interno
                    if (ex.InnerException != null)
                    {
                        Debug.WriteLine($"Inner Exception: {ex.InnerException.Message}");
                        Console.WriteLine($"Inner Exception: {ex.InnerException.Message}");
                        _lastErrorMessage += $"\nDetalle: {ex.InnerException.Message}";
                    }
                }
                catch (Exception ex)
                {
                    _lastErrorMessage = $"Error inesperado: {ex.GetType().Name} - {ex.Message}";
                    Debug.WriteLine($"Error tipo: {ex.GetType().Name}");
                    Debug.WriteLine($"Error al conectar: {ex.Message}");
                    Debug.WriteLine($"StackTrace: {ex.StackTrace}");

                    // Si no es el último intento, esperar antes de reintentar
                    if (attempts < maxAttempts)
                    {
                        Debug.WriteLine($"Esperando 1 segundo antes del reintento {attempts + 1}...");
                        await Task.Delay(1000);
                        continue;
                    }

                    // Mostrar detalles de error interno
                    if (ex.InnerException != null)
                    {
                        Debug.WriteLine($"Inner Exception: {ex.InnerException.Message}");
                        Console.WriteLine($"Inner Exception: {ex.InnerException.Message}");
                        _lastErrorMessage += $"\nDetalle: {ex.InnerException.Message}";
                    }
                }
            }

            // Si llegamos aquí después de varios intentos, notificar cambio de estado
            if (_isConnected)
            {
                _isConnected = false;
                ConnectionStatusChanged?.Invoke(this, false);
                Debug.WriteLine("Estado de conexión cambiado a: DESCONECTADO (después de múltiples intentos)");
            }

            // Mostrar popup detallado solo en el último intento fallido
            if (AppSettings.ShowDetailedErrors)
            {
                await Task.Run(() => {
                    MessageBox.Show(
                        $"No se pudo conectar con el servidor después de {maxAttempts} intentos.\n\nURL: {_baseUrl}/health\n\nError: {_lastErrorMessage}",
                        "Error de Conexión",
                        MessageBoxButton.OK,
                        MessageBoxImage.Error
                    );
                });
            }

            return false;
        }

        public async Task<bool> SendMonitorData(MonitorData data)
        {
            try
            {
                // Verificar campos obligatorios
                if (string.IsNullOrEmpty(data.ActivisionId) || data.ChannelId <= 0)
                {
                    _lastErrorMessage = "Error: ActivisionId y ChannelId son obligatorios";
                    Console.WriteLine(_lastErrorMessage);
                    return false;
                }

                // Transformar procesos para asegurar que usan el formato camelCase esperado por el servidor
                var processesFormatted = data.Processes.Select(p => new
                {
                    name = p.Name,
                    id = p.Pid,  // Cambiar pid a id
                    filePath = p.FilePath,
                    fileHash = p.FileHash,
                    commandLine = p.CommandLine,
                    fileVersion = p.FileVersion,
                    isSigned = p.IsSigned,
                    signatureInfo = p.SignatureInfo,
                    memoryUsage = p.MemoryUsage,
                    startTime = p.StartTime
                }).ToList();

                // Transformar dispositivos USB
                var usbDevicesFormatted = data.UsbDevices.Select(d => new
                {
                    deviceId = d.DeviceId,
                    name = d.Name,
                    description = d.Description,
                    manufacturer = d.Manufacturer,
                    type = d.Type,
                    status = d.Status,
                    connectionStatus = d.ConnectionStatus,
                    deviceClass = d.DeviceClass,
                    classGuid = d.ClassGuid,
                    driver = d.Driver,
                    hardwareId = d.HardwareId,
                    locationInfo = d.LocationInfo,
                    trustLevel = d.TrustLevel
                }).ToList();

                // Transformar hardware info
                var hardwareInfoFormatted = new
                {
                    cpu = data.HardwareInfo.Cpu,
                    gpu = data.HardwareInfo.Gpu,
                    gpuDriverVersion = data.HardwareInfo.GpuDriverVersion,
                    ram = data.HardwareInfo.Ram,
                    motherboard = data.HardwareInfo.Motherboard,
                    storage = data.HardwareInfo.Storage,
                    networkAdapters = data.HardwareInfo.NetworkAdapters,
                    audioDevices = data.HardwareInfo.AudioDevices,
                    biosVersion = data.HardwareInfo.BiosVersion,
                    hardwareId = data.HardwareInfo.HardwareId
                };

                // Transformar system info
                var systemInfoFormatted = new
                {
                    windowsVersion = data.SystemInfo.WindowsVersion,
                    directXVersion = data.SystemInfo.DirectXVersion,
                    gpuDriverVersion = data.SystemInfo.GpuDriverVersion,
                    screenResolution = data.SystemInfo.ScreenResolution,
                    windowsUsername = data.SystemInfo.WindowsUsername,
                    computerName = data.SystemInfo.ComputerName,
                    windowsInstallDate = data.SystemInfo.WindowsInstallDate,
                    lastBootTime = data.SystemInfo.LastBootTime,
                    firmwareType = data.SystemInfo.FirmwareType,
                    languageSettings = data.SystemInfo.LanguageSettings,
                    timeZone = data.SystemInfo.TimeZone,
                    frameworkVersion = data.SystemInfo.FrameworkVersion
                };

                // Transformar conexiones de red
                var networkConnectionsFormatted = data.NetworkConnections.Select(n => new
                {
                    localAddress = n.LocalAddress,
                    localPort = n.LocalPort,
                    remoteAddress = n.RemoteAddress,
                    remotePort = n.RemotePort,
                    protocol = n.Protocol,
                    state = n.State,
                    processId = n.ProcessId,
                    processName = n.ProcessName
                }).ToList();

                // Transformar drivers cargados
                var loadedDriversFormatted = data.LoadedDrivers.Select(d => new
                {
                    name = d.Name,
                    displayName = d.DisplayName,
                    description = d.Description,
                    pathName = d.PathName,
                    version = d.Version,
                    isSigned = d.IsSigned,
                    signatureInfo = d.SignatureInfo,
                    startType = d.StartType,
                    state = d.State
                }).ToList();

                // Crear objeto con la estructura exacta que espera el servidor, usando camelCase
                var requestData = new
                {
                    activisionId = data.ActivisionId,
                    channelId = data.ChannelId,
                    timestamp = data.Timestamp,
                    clientStartTime = data.ClientStartTime,
                    pcStartTime = data.PcStartTime,
                    isGameRunning = data.IsGameRunning,
                    processes = processesFormatted,
                    usbDevices = usbDevicesFormatted,
                    hardwareInfo = hardwareInfoFormatted,
                    systemInfo = systemInfoFormatted,
                    networkConnections = networkConnectionsFormatted,
                    loadedDrivers = loadedDriversFormatted
                };

                string json = JsonConvert.SerializeObject(requestData);
                if (AppSettings.DebugMode)
                {
                    Debug.WriteLine($"Enviando datos: {json.Substring(0, Math.Min(json.Length, 200))}..."); // Log para depuración (primeros 200 caracteres)
                }
                Console.WriteLine($"Enviando datos al endpoint {_baseUrl}/monitor");

                var content = new StringContent(json, Encoding.UTF8, "application/json");

                // Usar un CancellationToken para controlar el timeout
                var cancellationToken = new CancellationTokenSource(TimeSpan.FromSeconds(AppSettings.ApiTimeoutSeconds)).Token;

                var response = await _httpClient.PostAsync($"{_baseUrl}/monitor", content, cancellationToken);

                if (!response.IsSuccessStatusCode)
                {
                    string responseContent = await response.Content.ReadAsStringAsync();
                    _lastErrorMessage = $"Error del servidor: {(int)response.StatusCode} {response.ReasonPhrase}";
                    Debug.WriteLine(_lastErrorMessage);
                    Debug.WriteLine($"Detalle de error: {responseContent}");
                    Console.WriteLine(_lastErrorMessage);

                    // Si obtenemos un error 401/403/407, podría ser un problema de autenticación
                    if ((int)response.StatusCode == 401 || (int)response.StatusCode == 403 || (int)response.StatusCode == 407)
                    {
                        _lastErrorMessage += "\nPosible problema de autenticación o autorización con el servidor.";
                    }
                    // Si es un error 404, la URL podría estar mal
                    else if ((int)response.StatusCode == 404)
                    {
                        _lastErrorMessage += "\nEndpoint no encontrado. Verifica la URL del API.";
                    }
                    // Si es un error 5xx, es un problema del servidor
                    else if ((int)response.StatusCode >= 500)
                    {
                        _lastErrorMessage += "\nError interno del servidor. Contacta al administrador.";
                    }
                }
                else
                {
                    Debug.WriteLine("Datos enviados correctamente");
                    Console.WriteLine("Datos enviados correctamente");
                }

                return response.IsSuccessStatusCode;
            }
            catch (TaskCanceledException)
            {
                _lastErrorMessage = $"Tiempo de espera agotado ({AppSettings.ApiTimeoutSeconds} segundos) al enviar datos al servidor";
                Debug.WriteLine(_lastErrorMessage);
                Console.WriteLine(_lastErrorMessage);
                return false;
            }
            catch (HttpRequestException ex)
            {
                _lastErrorMessage = $"Error de red enviando datos: {ex.Message}";
                Debug.WriteLine(_lastErrorMessage);
                Console.WriteLine(_lastErrorMessage);
                if (ex.InnerException != null)
                {
                    Debug.WriteLine($"Inner Exception: {ex.InnerException.Message}");
                }
                return false;
            }
            catch (Exception ex)
            {
                _lastErrorMessage = $"Error enviando datos de monitoreo: {ex.GetType().Name} - {ex.Message}";
                Debug.WriteLine(_lastErrorMessage);
                Console.WriteLine(_lastErrorMessage);
                if (ex.InnerException != null)
                {
                    Debug.WriteLine($"Inner Exception: {ex.InnerException.Message}");
                }
                return false;
            }
        }

        public async Task<bool> SendGameStatus(string activisionId, int channelId, bool isGameRunning)
        {
            try
            {
                // Verificar campos obligatorios
                if (string.IsNullOrEmpty(activisionId) || channelId <= 0)
                {
                    _lastErrorMessage = "Error: ActivisionId y ChannelId son obligatorios";
                    Console.WriteLine(_lastErrorMessage);
                    return false;
                }

                var data = new
                {
                    activisionId, // Ya en camelCase
                    channelId,    // Ya en camelCase
                    isGameRunning // Ya en camelCase
                };

                string json = JsonConvert.SerializeObject(data);
                var content = new StringContent(json, Encoding.UTF8, "application/json");

                Debug.WriteLine($"Enviando estado del juego a {_baseUrl}/game-status");
                Console.WriteLine($"Enviando estado del juego a {_baseUrl}/game-status");

                // Usar un CancellationToken para controlar el timeout
                var cancellationToken = new CancellationTokenSource(TimeSpan.FromSeconds(AppSettings.ApiTimeoutSeconds)).Token;

                var response = await _httpClient.PostAsync($"{_baseUrl}/game-status", content, cancellationToken);

                if (!response.IsSuccessStatusCode)
                {
                    string responseContent = await response.Content.ReadAsStringAsync();
                    _lastErrorMessage = $"Error enviando estado del juego: {(int)response.StatusCode} {response.ReasonPhrase}";
                    Debug.WriteLine(_lastErrorMessage);
                    Debug.WriteLine($"Detalle: {responseContent}");
                    Console.WriteLine(_lastErrorMessage);
                }
                else
                {
                    Debug.WriteLine("Estado del juego enviado correctamente");
                    Console.WriteLine("Estado del juego enviado correctamente");
                }

                return response.IsSuccessStatusCode;
            }
            catch (TaskCanceledException)
            {
                _lastErrorMessage = $"Tiempo de espera agotado ({AppSettings.ApiTimeoutSeconds} segundos) al enviar estado del juego";
                Debug.WriteLine(_lastErrorMessage);
                Console.WriteLine(_lastErrorMessage);
                return false;
            }
            catch (Exception ex)
            {
                _lastErrorMessage = $"Error enviando estado del juego: {ex.GetType().Name} - {ex.Message}";
                Debug.WriteLine(_lastErrorMessage);
                Console.WriteLine(_lastErrorMessage);
                if (ex.InnerException != null)
                {
                    Debug.WriteLine($"Inner Exception: {ex.InnerException.Message}");
                }
                return false;
            }
        }

        public async Task<bool> SendScreenshot(string activisionId, int channelId, string screenshotBase64)
        {
            try
            {
                // Verificar campos obligatorios
                if (string.IsNullOrEmpty(activisionId) || channelId <= 0 || string.IsNullOrEmpty(screenshotBase64))
                {
                    _lastErrorMessage = "Error: ActivisionId, ChannelId y screenshot son obligatorios";
                    Console.WriteLine(_lastErrorMessage);
                    return false;
                }

                var data = new
                {
                    activisionId, // Ya en camelCase
                    channelId,    // Ya en camelCase
                    timestamp = DateTime.Now,
                    screenshot = screenshotBase64
                };

                string json = JsonConvert.SerializeObject(data);
                var content = new StringContent(json, Encoding.UTF8, "application/json");

                Debug.WriteLine($"Enviando screenshot a {_baseUrl}/screenshot");
                Console.WriteLine($"Enviando screenshot a {_baseUrl}/screenshot");

                // Usar un CancellationToken para controlar el timeout
                var cancellationToken = new CancellationTokenSource(TimeSpan.FromSeconds(AppSettings.ApiTimeoutSeconds * 2)).Token; // Doble timeout para screenshots

                var response = await _httpClient.PostAsync($"{_baseUrl}/screenshot", content, cancellationToken);

                if (!response.IsSuccessStatusCode)
                {
                    string responseContent = await response.Content.ReadAsStringAsync();
                    _lastErrorMessage = $"Error enviando screenshot: {(int)response.StatusCode} {response.ReasonPhrase}";
                    Debug.WriteLine(_lastErrorMessage);
                    Debug.WriteLine($"Detalle: {responseContent}");
                    Console.WriteLine(_lastErrorMessage);
                }
                else
                {
                    Debug.WriteLine("Screenshot enviado correctamente");
                    Console.WriteLine("Screenshot enviado correctamente");
                }

                return response.IsSuccessStatusCode;
            }
            catch (TaskCanceledException)
            {
                _lastErrorMessage = $"Tiempo de espera agotado ({AppSettings.ApiTimeoutSeconds * 2} segundos) al enviar screenshot";
                Debug.WriteLine(_lastErrorMessage);
                Console.WriteLine(_lastErrorMessage);
                return false;
            }
            catch (Exception ex)
            {
                _lastErrorMessage = $"Error enviando screenshot: {ex.GetType().Name} - {ex.Message}";
                Debug.WriteLine(_lastErrorMessage);
                Console.WriteLine(_lastErrorMessage);
                if (ex.InnerException != null)
                {
                    Debug.WriteLine($"Inner Exception: {ex.InnerException.Message}");
                }
                return false;
            }
        }

        public async Task<bool> ReportError(string errorMessage)
        {
            try
            {
                var data = new
                {
                    error = errorMessage,
                    timestamp = DateTime.Now
                };

                string json = JsonConvert.SerializeObject(data);
                var content = new StringContent(json, Encoding.UTF8, "application/json");

                Debug.WriteLine($"Reportando error a {_baseUrl}/error");

                // Usar un CancellationToken para controlar el timeout
                var cancellationToken = new CancellationTokenSource(TimeSpan.FromSeconds(AppSettings.ApiTimeoutSeconds)).Token;

                var response = await _httpClient.PostAsync($"{_baseUrl}/error", content, cancellationToken);

                if (!response.IsSuccessStatusCode)
                {
                    _lastErrorMessage = $"Error al reportar error: {(int)response.StatusCode} {response.ReasonPhrase}";
                    Debug.WriteLine(_lastErrorMessage);
                }
                else
                {
                    Debug.WriteLine("Error reportado correctamente");
                }

                return response.IsSuccessStatusCode;
            }
            catch (Exception ex)
            {
                _lastErrorMessage = $"Excepción al reportar error: {ex.GetType().Name} - {ex.Message}";
                Debug.WriteLine(_lastErrorMessage);
                return false;
            }
        }
    }
}