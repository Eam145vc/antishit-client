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
        private string _pcStartTime;

        public MainOverlay(string activisionId, int channelId)
        {
            InitializeComponent();

            _activisionId = activisionId;
            _channelId = channelId;

            // Establecer las variables estáticas
            CurrentActivisionId = activisionId;
            CurrentChannelId = channelId;
            ClientStartTime = DateTime.Now;

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

            // Inicializar monitoreo
            InitializeMonitoring();

            // Manejar eventos
            _apiService.ConnectionStatusChanged += ApiService_ConnectionStatusChanged;
            _deviceMonitor.DeviceChanged += DeviceMonitor_DeviceChanged;
            _screenshotService.ScreenshotTaken += ScreenshotService_ScreenshotTaken;

            // Evitar que se cierre la aplicación al cerrar la ventana
            this.Closing += MainOverlay_Closing;
        }

        private void PositionWindowAtTopCenter()
        {
            // Posicionar la ventana en la parte superior central de la pantalla principal
            double screenWidth = SystemParameters.PrimaryScreenWidth;
            this.Left = (screenWidth - this.Width) / 2;
            this.Top = 0;
        }

        private void InitializeTimers()
        {
            // Timer para actualizar el estado de conexión
            _statusUpdateTimer = new DispatcherTimer();
            _statusUpdateTimer.Interval = TimeSpan.FromSeconds(10);
            _statusUpdateTimer.Tick += StatusUpdateTimer_Tick;
            _statusUpdateTimer.Start();

            // Timer para enviar datos de monitoreo
            _monitorTimer = new DispatcherTimer();
            _monitorTimer.Interval = TimeSpan.FromMilliseconds(AppSettings.MonitorIntervalMs);
            _monitorTimer.Tick += MonitorTimer_Tick;
            _monitorTimer.Start();
        }

        private async void InitializeMonitoring()
        {
            // Verificar estado de conexión inicial
            bool isConnected = await _apiService.CheckConnection();
            UpdateConnectionStatus(isConnected);

            // Inicializar monitoreo de dispositivos
            _deviceMonitor.Initialize();

            // Enviar datos iniciales de monitoreo
            await SendMonitorData();
        }

        private void UpdateConnectionStatus(bool isConnected)
        {
            Dispatcher.Invoke(() =>
            {
                statusIndicator.Fill = isConnected ? Brushes.Green : Brushes.Red;
            });
        }

        private async void StatusUpdateTimer_Tick(object sender, EventArgs e)
        {
            bool isConnected = await _apiService.CheckConnection();
            UpdateConnectionStatus(isConnected);
        }

        private async void MonitorTimer_Tick(object sender, EventArgs e)
        {
            await SendMonitorData();
        }

        private async Task SendMonitorData()
        {
            try
            {
                // Usar el método simplificado que ya incluye todos los datos
                await _monitorService.SendMonitorData();
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error enviando datos de monitoreo: {ex.Message}");
            }
        }

        private void ApiService_ConnectionStatusChanged(object sender, bool isConnected)
        {
            UpdateConnectionStatus(isConnected);
        }

        private void DeviceMonitor_DeviceChanged(object sender, DeviceChangedEventArgs e)
        {
            // Notificar al servidor sobre el cambio de dispositivo
            Task.Run(async () =>
            {
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
            });
        }

        private void ScreenshotService_ScreenshotTaken(object sender, string message)
        {
            // Podría mostrar una notificación temporal aquí
        }

        private async void BtnScreenshot_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                btnScreenshot.IsEnabled = false;
                btnScreenshot.Content = "Capturando...";

                await _screenshotService.CaptureAndSendScreenshot(_activisionId, _channelId);

                btnScreenshot.Content = "Enviado ✓";
                await Task.Delay(2000); // Mostrar "Enviado" por 2 segundos
            }
            catch
            {
                btnScreenshot.Content = "Error";
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
            ShowCloseWarning();
        }

        private void MainOverlay_Closing(object sender, System.ComponentModel.CancelEventArgs e)
        {
            // La advertencia será mostrada por el botón de cierre
            // Aquí solo reportamos al servidor que el usuario cerró la aplicación
            ReportClientClosed();
        }

        private void ShowCloseWarning()
        {
            MessageBoxResult result = MessageBox.Show(
                Constants.Messages.CloseWarning,
                "Advertencia",
                MessageBoxButton.YesNo,
                MessageBoxImage.Warning);

            if (result == MessageBoxResult.Yes)
            {
                // Notificar al servidor que el cliente se cerrará
                ReportClientClosed();

                // Cerrar la aplicación
                Application.Current.Shutdown();
            }
        }

        private async void ReportClientClosed()
        {
            try
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
            }
            catch
            {
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
                    return $"{ts.Days}d {ts.Hours}h {ts.Minutes}m {ts.Seconds}s";
                }
            }
            catch
            {
                return "Unknown";
            }
        }

        private void ShowNotification(string message)
        {
            // Simplemente mostrar un MessageBox por ahora
            // En una implementación más avanzada, podría ser un Toast no intrusivo
            MessageBox.Show(message, "Anti-Cheat Monitor", MessageBoxButton.OK, MessageBoxImage.Information);
        }
    }
}