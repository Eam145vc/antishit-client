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
using System.Windows.Controls;

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
        private ToolTip _statusTooltip;

        private DispatcherTimer _statusUpdateTimer;
        private DispatcherTimer _monitorTimer;
        private string _pcStartTime;

        public MainOverlay(string activisionId, int channelId)
        {
            try
            {
                Debug.WriteLine($"Iniciando MainOverlay con ID: {activisionId}, Canal: {channelId}");
                Console.WriteLine($"Iniciando MainOverlay con ID: {activisionId}, Canal: {channelId}");

                InitializeComponent();

                _activisionId = activisionId;
                _channelId = channelId;

                // Establecer las variables estáticas
                CurrentActivisionId = activisionId;
                CurrentChannelId = channelId;
                ClientStartTime = DateTime.Now;

                // Configurar tooltip para el indicador de estado
                _statusTooltip = new ToolTip();
                _statusTooltip.Content = "Estado de conexión: Verificando...";
                statusIndicator.ToolTip = _statusTooltip;

                Debug.WriteLine("MainOverlay - Inicializando servicios");

                // Inicializar servicios
                _apiService = new ApiService(AppSettings.ApiBaseUrl);
                _screenshotService = new ScreenshotService(_apiService);
                _monitorService = new MonitorService(_apiService);
                _deviceMonitor = new DeviceMonitor();

                // Configurar temporizadores
                InitializeTimers();

                // Obtener hora de inicio
                _pcStartTime = GetSystemUptimeString();

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
                Console.WriteLine($"ERROR EN MAINOVERLAY: {ex.Message}\n{ex.StackTrace}");

                // Mostrar el error en un cuadro de diálogo para asegurar que sea visible
                MessageBox.Show(
                    $"Error crítico al inicializar MainOverlay:\n{ex.Message}\n\nDetalles:\n{ex.StackTrace}",
                    "Error de Inicialización",
                    MessageBoxButton.OK,
                    MessageBoxImage.Error
                );
            }
        }

        private void DiagButton_Click(object sender, RoutedEventArgs e)
        {
            ShowConnectionDiagnosticInfo();
        }

        private void ShowConnectionDiagnosticInfo()
        {
            string statusText = _apiService.IsConnected ? "CONECTADO" : "DESCONECTADO";
            string errorDetails = string.IsNullOrEmpty(_apiService.LastErrorMessage)
                                ? "No hay errores registrados"
                                : _apiService.LastErrorMessage;

            string message = $"DIAGNÓSTICO DE CONEXIÓN\n\n" +
                            $"URL del API: {AppSettings.ApiBaseUrl}\n" +
                            $"Estado actual: {statusText}\n" +
                            $"Último error: {errorDetails}\n\n" +
                            $"ID de Activision: {_activisionId}\n" +
                            $"Canal: {_channelId}\n" +
                            $"Tiempo de ejecución: {DateTime.Now - ClientStartTime}\n\n" +
                            $"¿Desea intentar reconectar ahora?";

            var result = MessageBox.Show(
                message,
                "Diagnóstico de Conexión",
                MessageBoxButton.YesNo,
                MessageBoxImage.Information
            );

            if (result == MessageBoxResult.Yes)
            {
                ForceConnectionCheck();
            }
        }

        private async void ForceConnectionCheck()
        {
            try
            {
                UpdateConnectionStatus(false, "Verificando...");
                bool isConnected = await _apiService.CheckConnection();
                UpdateConnectionStatus(isConnected, _apiService.LastErrorMessage);

                if (!isConnected)
                {
                    MessageBox.Show(
                        $"No se pudo establecer conexión con el servidor.\n\nError: {_apiService.LastErrorMessage}",
                        "Error de Conexión",
                        MessageBoxButton.OK,
                        MessageBoxImage.Error
                    );
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Error forzando verificación de conexión: {ex.Message}");
                UpdateConnectionStatus(false, $"Error: {ex.Message}");
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
                _statusUpdateTimer.Interval = TimeSpan.FromSeconds(30); // Comprobar cada 30 segundos
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
                Console.WriteLine($"Error al inicializar temporizadores: {ex.Message}");
            }
        }

        private async void InitializeMonitoring()
        {
            try
            {
                Debug.WriteLine("Inicializando monitoreo");

                // Verificar estado de conexión inicial
                UpdateConnectionStatus(false, "Verificando conexión inicial...");
                bool isConnected = await _apiService.CheckConnection();
                UpdateConnectionStatus(isConnected, _apiService.LastErrorMessage);

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
                Console.WriteLine($"Error al inicializar monitoreo: {ex.Message}");
                UpdateConnectionStatus(false, $"Error: {ex.Message}");
            }
        }

        private void UpdateConnectionStatus(bool isConnected, string statusMessage = "")
        {
            try
            {
                Dispatcher.Invoke(() =>
                {
                    statusIndicator.Fill = isConnected ? Brushes.Green : Brushes.Red;

                    // Actualizar tooltip con información de estado
                    string statusText = isConnected ? "CONECTADO" : "DESCONECTADO";
                    string tooltipText = $"Estado: {statusText}";

                    if (!string.IsNullOrEmpty(statusMessage))
                    {
                        tooltipText += $"\n{statusMessage}";
                    }

                    _statusTooltip.Content = tooltipText;

                    Debug.WriteLine($"Indicador de estado actualizado a: {statusText}");
                    if (!string.IsNullOrEmpty(statusMessage))
                    {
                        Debug.WriteLine($"Mensaje de estado: {statusMessage}");
                    }
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
                bool isConnected = await _apiService.CheckConnection();
                UpdateConnectionStatus(isConnected, _apiService.LastErrorMessage);
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Error en StatusUpdateTimer_Tick: {ex.Message}");
                UpdateConnectionStatus(false, $"Error: {ex.Message}");
            }
        }

        private async void MonitorTimer_Tick(object sender, EventArgs e)
        {
            try
            {
                Debug.WriteLine("Enviando datos de monitoreo periódicamente");
                await SendMonitorData();
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Error en MonitorTimer_Tick: {ex.Message}");
            }
        }

        private async Task SendMonitorData()
        {
            try
            {
                Debug.WriteLine("Enviando datos de monitoreo al servidor");
                // Solo intentamos enviar datos si estamos conectados
                if (_apiService.IsConnected)
                {
                    bool result = await _monitorService.SendMonitorData();
                    Debug.WriteLine($"Resultado del envío de datos: {(result ? "Exitoso" : "Fallido")}");

                    // Si falló, actualizar el estado de conexión
                    if (!result)
                    {
                        UpdateConnectionStatus(false, "Error enviando datos al servidor: " + _apiService.LastErrorMessage);
                    }
                }
                else
                {
                    Debug.WriteLine("No se enviaron datos porque no hay conexión con el servidor");
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Error enviando datos de monitoreo: {ex.Message}");
                Console.WriteLine($"Error enviando datos de monitoreo: {ex.Message}");
                UpdateConnectionStatus(false, $"Error: {ex.Message}");
            }
        }

        private void ApiService_ConnectionStatusChanged(object sender, bool isConnected)
        {
            Debug.WriteLine($"Estado de conexión cambiado a: {(isConnected ? "Conectado" : "Desconectado")}");
            UpdateConnectionStatus(isConnected, _apiService.LastErrorMessage);
        }

        private void DeviceMonitor_DeviceChanged(object sender, DeviceChangedEventArgs e)
        {
            // Notificar al servidor sobre el cambio de dispositivo
            Task.Run(async () =>
            {
                try
                {
                    Debug.WriteLine($"Dispositivo {(e.IsConnected ? "conectado" : "desconectado")}: {e.Device.Name}");

                    // Enviar datos actualizados al servidor solo si hay conexión
                    if (_apiService.IsConnected)
                    {
                        await SendMonitorData();
                    }

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

                if (!_apiService.IsConnected)
                {
                    MessageBox.Show(
                        "No se puede enviar la captura de pantalla porque no hay conexión con el servidor.\n\n" +
                        "Verifique su conexión e intente nuevamente.",
                        "Error de Conexión",
                        MessageBoxButton.OK,
                        MessageBoxImage.Warning
                    );

                    btnScreenshot.Content = "Sin conexión";
                    await Task.Delay(2000);
                }
                else
                {
                    bool result = await _screenshotService.CaptureAndSendScreenshot(_activisionId, _channelId);

                    if (result)
                    {
                        btnScreenshot.Content = "Enviado ✓";
                        Debug.WriteLine("Screenshot enviado correctamente");
                    }
                    else
                    {
                        btnScreenshot.Content = "Error ✗";
                        Debug.WriteLine("Error enviando screenshot");

                        MessageBox.Show(
                            "Error al enviar la captura de pantalla al servidor.\n\n" +
                            $"Error: {_apiService.LastErrorMessage}",
                            "Error",
                            MessageBoxButton.OK,
                            MessageBoxImage.Error
                        );
                    }

                    await Task.Delay(2000);
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Error al capturar pantalla: {ex.Message}");
                btnScreenshot.Content = "Error";

                MessageBox.Show(
                    $"Error al capturar pantalla: {ex.Message}",
                    "Error",
                    MessageBoxButton.OK,
                    MessageBoxImage.Error
                );

                await Task.Delay(2000);
            }
            finally
            {
                btnScreenshot.IsEnabled = true;
                btnScreenshot.Content = "Capturar pantalla";
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
                // Solo reportar si hay conexión
                if (_apiService.IsConnected)
                {
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
                else
                {
                    Debug.WriteLine("No se pudo reportar cierre porque no hay conexión");
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Error al reportar cierre: {ex.Message}");
                // Ignorar errores al reportar el cierre
            }
        }

        private string GetSystemUptimeString()
        {
            try
            {
                // Obtener tiempo de inicio del sistema
                using (var uptime = new PerformanceCounter("System", "System Up Time"))
                {
                    uptime.NextValue(); // Primera llamada siempre retorna 0
                    TimeSpan ts = TimeSpan.FromSeconds(uptime.NextValue());
                    var result = $"{ts.Days}d {ts.Hours}h {ts.Minutes}m {ts.Seconds}s";
                    Debug.WriteLine($"Tiempo de inicio del sistema: {result}");
                    return result;
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Error al obtener tiempo de inicio: {ex.Message}");
                return "Unknown";
            }
        }

        private void ShowNotification(string message)
        {
            Debug.WriteLine($"Mostrando notificación: {message}");
            // Simplemente mostrar un MessageBox por ahora
            // En una implementación más avanzada, podría ser un Toast no intrusivo
            MessageBox.Show(message, "Anti-Cheat Monitor", MessageBoxButton.OK, MessageBoxImage.Information);
        }
    }
}