// Path: Anti-shitX/UI/Windows/MainOverlay.xaml.cs

using System;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Media;
using System.Windows.Threading;
using Newtonsoft.Json;
using AntiCheatClient.Core.Config;
using AntiCheatClient.Core.Models;
using AntiCheatClient.Core.Services;
using AntiCheatClient.DetectionEngine;
using System.Diagnostics;

namespace AntiCheatClient.UI.Windows
{
    public partial class MainOverlay : Window
    {
        // Variables estáticas para compartir con otros servicios
        public static string CurrentActivisionId { get; private set; }
        public static int CurrentChannelId { get; private set; }
        public static DateTime ClientStartTime { get; private set; }

        private readonly string _activisionId;
        private readonly int _channelId;
        private readonly ApiService _apiService;
        private readonly MonitorService _monitorService;
        private readonly ScreenshotService _screenshotService;
        private readonly DeviceMonitor _deviceMonitor;

        private DispatcherTimer _statusUpdateTimer;
        private DispatcherTimer _monitorTimer;

        public MainOverlay(string activisionId, int channelId)
        {
            try
            {
                Debug.WriteLine($"Iniciando MainOverlay con ID: {activisionId}, Canal: {channelId}");

                InitializeComponent();

                _activisionId = activisionId;
                _channelId = channelId;

                // Establecer las variables estáticas
                CurrentActivisionId = activisionId;
                CurrentChannelId = channelId;
                ClientStartTime = DateTime.Now;

                Debug.WriteLine("MainOverlay - Inicializando servicios");

                // Inicializar servicios
                _apiService = new ApiService(AppSettings.ApiBaseUrl);
                _screenshotService = new ScreenshotService(_apiService);
                _monitorService = new MonitorService(_apiService);
                _deviceMonitor = new DeviceMonitor();

                // Configurar temporizadores
                InitializeTimers();

                // Posicionar en parte superior central
                PositionWindowAtTopCenter();

                Debug.WriteLine("MainOverlay - Inicializando monitoreo");

                // Inicializar monitoreo
                InitializeMonitoring();

                // Manejar eventos
                _apiService.ConnectionStatusChanged += ApiService_ConnectionStatusChanged;
                _deviceMonitor.DeviceChanged += DeviceMonitor_DeviceChanged;
                _screenshotService.ScreenshotTaken += ScreenshotService_ScreenshotTaken;

                // Evitar que se cierre la aplicación al cerrar la ventana
                this.Closing += MainOverlay_Closing;

                Debug.WriteLine("MainOverlay - Inicialización completa");
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"ERROR EN MAINOVERLAY: {ex.Message}\n{ex.StackTrace}");

                MessageBox.Show(
                    $"Error crítico al inicializar MainOverlay:\n{ex.Message}\n\nDetalles:\n{ex.StackTrace}",
                    "Error de Inicialización",
                    MessageBoxButton.OK,
                    MessageBoxImage.Error
                );
            }
        }

        private void PositionWindowAtTopCenter()
        {
            try
            {
                // Posicionar la ventana en la parte superior central de la pantalla principal
                double screenWidth = SystemParameters.PrimaryScreenWidth;
                this.Left = (screenWidth - this.Width) / 2;
                this.Top = 0;

                Debug.WriteLine($"Ventana posicionada en: Left={this.Left}, Top={this.Top}");
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Error al posicionar ventana: {ex.Message}");
            }
        }

        private void InitializeTimers()
        {
            try
            {
                Debug.WriteLine("Inicializando temporizadores");

                // Timer para actualizar el estado de conexión
                _statusUpdateTimer = new DispatcherTimer();
                _statusUpdateTimer.Interval = TimeSpan.FromSeconds(30);
                _statusUpdateTimer.Tick += StatusUpdateTimer_Tick;
                _statusUpdateTimer.Start();

                // Timer para enviar datos de monitoreo
                _monitorTimer = new DispatcherTimer();
                _monitorTimer.Interval = TimeSpan.FromMilliseconds(AppSettings.MonitorIntervalMs);
                _monitorTimer.Tick += MonitorTimer_Tick;
                _monitorTimer.Start();

                Debug.WriteLine("Temporizadores iniciados correctamente");
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Error al inicializar temporizadores: {ex.Message}");
            }
        }

        private async void InitializeMonitoring()
        {
            try
            {
                Debug.WriteLine("Inicializando monitoreo");

                // Verificación inicial de conexión con retries
                bool isConnected = await RetryConnectionCheck(3);
                Debug.WriteLine($"Estado inicial de conexión: {(isConnected ? "Conectado" : "Desconectado")}");
                UpdateConnectionStatus(isConnected);

                // Inicializar monitoreo de dispositivos
                _deviceMonitor.Initialize();
                Debug.WriteLine("Monitor de dispositivos inicializado");

                // Enviar datos iniciales de monitoreo
                await SendMonitorData();
                Debug.WriteLine("Datos iniciales de monitoreo enviados");
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Error al inicializar monitoreo: {ex.Message}");
            }
        }

        private async Task<bool> RetryConnectionCheck(int maxRetries)
        {
            for (int attempt = 1; attempt <= maxRetries; attempt++)
            {
                try
                {
                    bool isConnected = await _apiService.CheckConnection();
                    if (isConnected) return true;

                    // Esperar un tiempo antes de reintentar
                    await Task.Delay(attempt * 2000); // Incrementa el tiempo de espera
                }
                catch
                {
                    // Ignorar errores en los intentos de conexión
                }
            }
            return false;
        }

        private void UpdateConnectionStatus(bool isConnected)
        {
            try
            {
                Dispatcher.Invoke(() =>
                {
                    statusIndicator.Fill = isConnected ? Brushes.Green : Brushes.Red;
                    Debug.WriteLine($"Indicador de estado actualizado a: {(isConnected ? "Verde (Conectado)" : "Rojo (Desconectado)")}");
                });
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Error al actualizar estado de conexión: {ex.Message}");
            }
        }

        private async void StatusUpdateTimer_Tick(object sender, EventArgs e)
        {
            try
            {
                Debug.WriteLine("Verificando estado de conexión periódicamente");

                // Usar método de reintento
                bool wasConnected = _apiService.IsConnected;
                bool isConnected = await RetryConnectionCheck(2);

                // Solo actualizar si cambia el estado
                if (wasConnected != isConnected)
                {
                    UpdateConnectionStatus(isConnected);

                    if (!isConnected)
                    {
                        // Intentar reconectar de forma más agresiva
                        await Task.Run(async () =>
                        {
                            for (int i = 0; i < 3; i++)
                            {
                                await Task.Delay(5000 * (i + 1)); // Incrementar tiempo entre intentos
                                bool reconnected = await RetryConnectionCheck(2);
                                if (reconnected)
                                {
                                    Dispatcher.Invoke(() =>
                                    {
                                        UpdateConnectionStatus(true);
                                        SendMonitorData(); // Enviar datos al reconectar
                                    });
                                    break;
                                }
                            }
                        });
                    }
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Error en StatusUpdateTimer_Tick: {ex.Message}");
            }
        }

        private async Task EnsureConnection()
        {
            if (!_apiService.IsConnected)
            {
                bool reconnected = await RetryConnectionCheck(3);
                if (reconnected)
                {
                    UpdateConnectionStatus(true);
                    await SendMonitorData();
                }
                else
                {
                    UpdateConnectionStatus(false);
                }
            }
        }

        private async void MonitorTimer_Tick(object sender, EventArgs e)
        {
            try
            {
                // Asegurar conexión antes de enviar datos
                await EnsureConnection();

                Debug.WriteLine("Enviando datos de monitoreo periódicamente");
                await SendMonitorData();
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Error en MonitorTimer_Tick: {ex.Message}");
            }
        }

        private async Task<bool> SendMonitorData()
        {
            try
            {
                Debug.WriteLine("Enviando datos de monitoreo al servidor");
                // Usar el método simplificado que ya incluye todos los datos
                bool result = await _monitorService.SendMonitorData();
                Debug.WriteLine($"Resultado del envío de datos: {(result ? "Exitoso" : "Fallido")}");
                return result;
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Error enviando datos de monitoreo: {ex.Message}");
                return false;
            }
        }

        private void ApiService_ConnectionStatusChanged(object sender, bool isConnected)
        {
            Debug.WriteLine($"Estado de conexión cambiado a: {(isConnected ? "Conectado" : "Desconectado")}");
            UpdateConnectionStatus(isConnected);
        }

        private void DeviceMonitor_DeviceChanged(object sender, DeviceChangedEventArgs e)
        {
            // Notificar al servidor sobre el cambio de dispositivo
            Task.Run(async () =>
            {
                try
                {
                    Debug.WriteLine($"Dispositivo {(e.IsConnected ? "conectado" : "desconectado")}: {e.Device.Name}");

                    // Enviar datos actualizados al servidor
                    await SendMonitorData();

                    // Mostrar notificación al usuario
                    Dispatcher.Invoke(() =>
                    {
                        string message = e.IsConnected
                            ? Constants.Messages.DeviceDetected
                            : Constants.Messages.DeviceRemoved;

                        ShowNotification(message);
                    });
                }
                catch (Exception ex)
                {
                    Debug.WriteLine($"Error en DeviceMonitor_DeviceChanged: {ex.Message}");
                }
            });
        }

        private void ScreenshotService_ScreenshotTaken(object sender, string message)
        {
            Debug.WriteLine($"Screenshot tomado: {message}");
            // Podría mostrar una notificación temporal aquí
        }

        private async void BtnScreenshot_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                Debug.WriteLine("Iniciando captura de pantalla");
                btnScreenshot.IsEnabled = false;
                btnScreenshot.Content = "Capturando...";

                await _screenshotService.CaptureAndSendScreenshot(_activisionId, _channelId);

                btnScreenshot.Content = "Enviado ✓";
                Debug.WriteLine("Screenshot enviado correctamente");
                await Task.Delay(2000); // Mostrar "Enviado" por 2 segundos
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Error al capturar pantalla: {ex.Message}");
                btnScreenshot.Content = "Error";
                await Task.Delay(2000);
            }
            finally
            {
                btnScreenshot.IsEnabled = true;
                btnScreenshot.Content = "Capturar pantalla";
            }
        }

        // Añadido método faltante DiagButton_Click
        private void DiagButton_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                Debug.WriteLine("Botón de diagnóstico presionado");

                // Mostrar información de estado de conexión
                string connectionStatus = _apiService.IsConnected ? "Conectado" : "Desconectado";
                string lastError = _apiService.LastErrorMessage;
                string apiUrl = AppSettings.ApiBaseUrl;
                string debugMode = AppSettings.DebugMode ? "Activado" : "Desactivado";

                string message = $"Estado de conexión: {connectionStatus}\n" +
                                $"URL del API: {apiUrl}\n" +
                                $"Modo depuración: {debugMode}\n" +
                                $"ID Activision: {_activisionId}\n" +
                                $"Canal: {_channelId}\n\n";

                if (!string.IsNullOrEmpty(lastError))
                {
                    message += $"Último error: {lastError}";
                }

                MessageBox.Show(
                    message,
                    "Información de Diagnóstico",
                    MessageBoxButton.OK,
                    MessageBoxImage.Information
                );
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Error en DiagButton_Click: {ex.Message}");
                MessageBox.Show(
                    $"Error al mostrar diagnóstico: {ex.Message}",
                    "Error",
                    MessageBoxButton.OK,
                    MessageBoxImage.Error
                );
            }
        }

        private void BtnClose_Click(object sender, RoutedEventArgs e)
        {
            Debug.WriteLine("Botón de cierre presionado");
            ShowCloseWarning();
        }

        private void MainOverlay_Closing(object sender, System.ComponentModel.CancelEventArgs e)
        {
            Debug.WriteLine("Evento de cierre activado");
            // La advertencia será mostrada por el botón de cierre
            // Aquí solo reportamos al servidor que el usuario cerró la aplicación
            ReportClientClosed();
        }

        private void ShowCloseWarning()
        {
            Debug.WriteLine("Mostrando advertencia de cierre");
            MessageBoxResult result = MessageBox.Show(
                Constants.Messages.CloseWarning,
                "Advertencia",
                MessageBoxButton.YesNo,
                MessageBoxImage.Warning);

            if (result == MessageBoxResult.Yes)
            {
                Debug.WriteLine("Usuario confirmó cierre de la aplicación");
                // Notificar al servidor que el cliente se cerrará
                ReportClientClosed();

                // Cerrar la aplicación
                Application.Current.Shutdown();
            }
            else
            {
                Debug.WriteLine("Usuario canceló cierre de la aplicación");
            }
        }

        private async void ReportClientClosed()
        {
            try
            {
                Debug.WriteLine("Reportando cierre del cliente al servidor");
                // Enviar un reporte final al servidor indicando que el cliente fue cerrado por el usuario
                var closeData = new
                {
                    activisionId = _activisionId,
                    channelId = _channelId,
                    timestamp = DateTime.Now,
                    reason = "UserClosed",
                    clientStartTime = ClientStartTime,
                    clientDuration = (DateTime.Now - ClientStartTime).TotalMinutes
                };

                // Intentar enviar el reporte, ignorando el resultado
                await _apiService.ReportError($"Cliente cerrado por usuario: {JsonConvert.SerializeObject(closeData)}");
                Debug.WriteLine("Reporte de cierre enviado");
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Error al reportar cierre: {ex.Message}");
                // Ignorar errores al reportar el cierre
            }
        }

        private void ShowNotification(string message)
        {
            Debug.WriteLine($"Mostrando notificación: {message}");
            // Simplemente mostrar un MessageBox por ahora
            MessageBox.Show(message, "Anti-Cheat Monitor", MessageBoxButton.OK, MessageBoxImage.Information);
        }
    }
}