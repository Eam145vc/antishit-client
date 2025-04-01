using System;
using System.Windows;
using AntiCheatClient.Core.Services;
using AntiCheatClient.Core.Config;

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

            // Configurar manejo de excepciones no controladas
            AppDomain.CurrentDomain.UnhandledException += CurrentDomain_UnhandledException;
            Current.DispatcherUnhandledException += Current_DispatcherUnhandledException;
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