using System;
using System.Windows;
using AntiCheatClient.Core.Services;
using AntiCheatClient.Core.Config;
using System.Threading.Tasks;

namespace AntiCheatClient
{
    public partial class App : Application
    {
        private ApiService _apiService;
        private MonitorService _monitorService;
        private ScreenshotService _screenshotService;

        protected override void OnStartup(StartupEventArgs e)
        {
            base.OnStartup(e);

            // Inicializar servicios principales
            _apiService = new ApiService(AppSettings.ApiBaseUrl);
            _monitorService = new MonitorService(_apiService);
            _screenshotService = new ScreenshotService(_apiService);

            // Configurar comprobación automática de solicitudes de capturas
            StartCheckingScreenshotRequests();

            // Configurar manejo de excepciones no controladas
            AppDomain.CurrentDomain.UnhandledException += CurrentDomain_UnhandledException;
            Current.DispatcherUnhandledException += Current_DispatcherUnhandledException;
        }

        private async void StartCheckingScreenshotRequests()
        {
            try
            {
                // Esperar un poco para asegurar que todo esté inicializado
                await Task.Delay(5000);

                while (true)
                {
                    try
                    {
                        // Comprobar si hay solicitudes pendientes para el jugador actual
                        if (!string.IsNullOrEmpty(UI.Windows.MainOverlay.CurrentActivisionId) &&
                            UI.Windows.MainOverlay.CurrentChannelId > 0)
                        {
                            string activisionId = UI.Windows.MainOverlay.CurrentActivisionId;
                            int channelId = UI.Windows.MainOverlay.CurrentChannelId;

                            // Verificar si hay solicitudes pendientes
                            bool hasRequest = await _apiService.CheckScreenshotRequests(activisionId, channelId);

                            if (hasRequest)
                            {
                                Console.WriteLine($"Solicitud de screenshot encontrada para {activisionId}. Capturando pantalla...");
                                // Indicar explícitamente que la fuente es "judge" para solicitudes remotas
                                await _screenshotService.CaptureAndSendScreenshot(activisionId, channelId, "judge");
                            }
                        }
                    }
                    catch (Exception ex)
                    {
                        Console.WriteLine($"Error en la verificación de solicitudes de captura: {ex.Message}");
                    }

                    // Esperar antes de la próxima verificación (cada 10 segundos)
                    await Task.Delay(10000);
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error iniciando el servicio de comprobación de capturas: {ex.Message}");
            }
        }

        private void CurrentDomain_UnhandledException(object sender, UnhandledExceptionEventArgs e)
        {
            LogException(e.ExceptionObject as Exception);
        }

        private void Current_DispatcherUnhandledException(object sender, System.Windows.Threading.DispatcherUnhandledExceptionEventArgs e)
        {
            LogException(e.Exception);
            e.Handled = true;
        }

        private void LogException(Exception ex)
        {
            try
            {
                string errorMessage = $"Error no controlado: {ex?.Message}\n{ex?.StackTrace}";
                MessageBox.Show(errorMessage, "Error", MessageBoxButton.OK, MessageBoxImage.Error);

                // Intentar reportar el error al servidor
                if (_apiService != null)
                {
                    // No hacemos await ya que es una operación asíncrona en un controlador de evento sincrónico
#pragma warning disable CS4014
                    _apiService.ReportError(errorMessage);
#pragma warning restore CS4014
                }
            }
            catch
            {
                // No hacer nada si falla el registro del error
            }
        }
    }
}