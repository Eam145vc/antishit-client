using System;
using System.Threading.Tasks;
using System.Windows;
using AntiCheatClient.Core.Services;
using AntiCheatClient.Core.Config;
using System.Windows.Controls;
using System.Diagnostics;
using System.Net.NetworkInformation;

namespace AntiCheatClient.UI.Windows
{
    public partial class LoginWindow : Window
    {
        private readonly ApiService _apiService;

        public string ActivisionId { get; private set; }
        public int ChannelId { get; private set; }

        public LoginWindow()
        {
            try
            {
                Debug.WriteLine("Iniciando LoginWindow");
                Console.WriteLine("Iniciando LoginWindow");

                InitializeComponent();

                _apiService = new ApiService(AppSettings.ApiBaseUrl);
                Debug.WriteLine($"ApiService inicializado con URL: {AppSettings.ApiBaseUrl}");

                cmbChannel.SelectedIndex = 0;

                // Mostrar mensaje sobre el estado de verificación de API
                if (AppSettings.SkipApiVerification)
                {
                    ShowError($"MODO SIN VERIFICACIÓN DE API ACTIVADO. URL: {AppSettings.ApiBaseUrl}");
                }
                else
                {
                    ShowError($"Conectando a: {AppSettings.ApiBaseUrl}");
                    // Verificar conectividad de red
                    CheckNetworkConnectivity();
                }

                Debug.WriteLine("LoginWindow inicializado correctamente");
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"ERROR EN CONSTRUCTOR LOGINWINDOW: {ex.Message}\n{ex.StackTrace}");
                Console.WriteLine($"ERROR EN CONSTRUCTOR LOGINWINDOW: {ex.Message}\n{ex.StackTrace}");

                MessageBox.Show(
                    $"Error al inicializar la ventana de login:\n{ex.Message}\n\nDetalles:\n{ex.StackTrace}",
                    "Error de Inicialización",
                    MessageBoxButton.OK,
                    MessageBoxImage.Error
                );
            }
        }

        private async void CheckNetworkConnectivity()
        {
            try
            {
                // Verificar si hay conexión a Internet
                bool hasInternet = NetworkInterface.GetIsNetworkAvailable();

                if (!hasInternet)
                {
                    ShowError("ADVERTENCIA: No se detecta conexión a Internet. Verifique su conectividad.");
                    return;
                }

                // Comprobar si podemos hacer ping a algunos servidores conocidos
                ShowError("Verificando conectividad de red...");

                bool pingSuccess = false;
                string[] testHosts = { "google.com", "microsoft.com", "cloudflare.com" };

                foreach (var host in testHosts)
                {
                    try
                    {
                        using (var ping = new Ping())
                        {
                            var reply = await ping.SendPingAsync(host, 3000);
                            if (reply.Status == IPStatus.Success)
                            {
                                pingSuccess = true;
                                ShowError($"Conectividad de red verificada (ping a {host}: {reply.RoundtripTime}ms)");
                                break;
                            }
                        }
                    }
                    catch (Exception ex)
                    {
                        Debug.WriteLine($"Error haciendo ping a {host}: {ex.Message}");
                    }
                }

                if (!pingSuccess)
                {
                    ShowError("ADVERTENCIA: No se pudo hacer ping a servidores externos. Posible problema de conectividad.");
                }

                // Verificar conexión con nuestro API
                bool apiConnected = await _apiService.CheckConnection();
                if (apiConnected)
                {
                    ShowError("✓ Conexión con API establecida correctamente");
                }
                else
                {
                    ShowError("✗ No se pudo conectar con la API. Se procederá en modo sin verificación.");

                    // Activar automáticamente el modo sin verificación si no se puede conectar
                    AppSettings.SkipApiVerification = true;

                    MessageBox.Show(
                        "No se pudo establecer conexión con el servidor API. La aplicación funcionará en modo sin verificación." +
                        "\n\nEsto significa que el monitoreo funcionará localmente, pero no enviará datos al servidor." +
                        "\n\nVerifique su conexión a Internet o contacte al administrador.",
                        "Aviso: Modo Sin Verificación",
                        MessageBoxButton.OK,
                        MessageBoxImage.Warning
                    );
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Error verificando red: {ex.Message}");
                ShowError("Error verificando conectividad de red.");
            }
        }

        private async void BtnConnect_Click(object sender, RoutedEventArgs e)
        {
            Debug.WriteLine("Botón 'Conectar' presionado");
            ShowError("Iniciando conexión...");

            // Validar entrada
            if (string.IsNullOrWhiteSpace(txtActivisionId.Text))
            {
                ShowError("Por favor ingresa tu ID de Activision.");
                Debug.WriteLine("Error: ID de Activision vacío");
                return;
            }

            if (cmbChannel.SelectedItem == null)
            {
                ShowError("Por favor selecciona un canal.");
                Debug.WriteLine("Error: Canal no seleccionado");
                return;
            }

            try
            {
                // Deshabilitar botón durante la conexión
                btnConnect.IsEnabled = false;
                btnConnect.Content = "Conectando...";
                Debug.WriteLine("Botón deshabilitado, iniciando conexión");

                // Obtener valores primero
                ActivisionId = txtActivisionId.Text.Trim();
                ChannelId = int.Parse(((ComboBoxItem)cmbChannel.SelectedItem).Tag.ToString());

                // Verificar la conexión con el API (a menos que se haya desactivado la verificación)
                bool apiConnected = AppSettings.SkipApiVerification;

                if (!AppSettings.SkipApiVerification)
                {
                    ShowError("Verificando conexión con API...");
                    apiConnected = await _apiService.CheckConnection();
                }

                if (apiConnected || AppSettings.SkipApiVerification)
                {
                    if (AppSettings.SkipApiVerification)
                    {
                        ShowError($"Modo sin verificación API activado. Omitiendo conexión a {AppSettings.ApiBaseUrl}");
                    }
                    else
                    {
                        ShowError($"Conectado a {AppSettings.ApiBaseUrl}");
                    }

                    Debug.WriteLine($"Iniciando MainOverlay con ID: {ActivisionId}, Canal: {ChannelId}");
                    ShowError($"Iniciando monitor con ID: {ActivisionId}, Canal: {ChannelId}");

                    // Iniciar el monitor
                    Debug.WriteLine("Creando instancia de MainOverlay");
                    MainOverlay mainOverlay = new MainOverlay(ActivisionId, ChannelId);

                    Debug.WriteLine("Mostrando MainOverlay");
                    mainOverlay.Show();

                    // Cerrar ventana de login
                    Debug.WriteLine("Cerrando ventana de login");
                    this.Close();
                }
                else
                {
                    ShowError("Error al conectar con el API. ¿Desea continuar de todos modos?");

                    MessageBoxResult result = MessageBox.Show(
                        "No se pudo establecer conexión con el servidor. Esto puede afectar la funcionalidad del monitor. ¿Desea continuar en modo sin verificación?",
                        "Error de Conexión",
                        MessageBoxButton.YesNo,
                        MessageBoxImage.Warning
                    );

                    if (result == MessageBoxResult.Yes)
                    {
                        // Proceder sin conexión API
                        AppSettings.SkipApiVerification = true;
                        ShowError($"Iniciando monitor sin conexión API. ID: {ActivisionId}, Canal: {ChannelId}");

                        // Iniciar el monitor
                        Debug.WriteLine("Creando instancia de MainOverlay (sin conexión API)");
                        MainOverlay mainOverlay = new MainOverlay(ActivisionId, ChannelId);

                        Debug.WriteLine("Mostrando MainOverlay");
                        mainOverlay.Show();

                        // Cerrar ventana de login
                        Debug.WriteLine("Cerrando ventana de login");
                        this.Close();
                    }
                    else
                    {
                        btnConnect.IsEnabled = true;
                        btnConnect.Content = "Conectar";
                    }
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"ERROR EN BTNCONNECT_CLICK: {ex.Message}\n{ex.StackTrace}");
                Console.WriteLine($"ERROR EN BTNCONNECT_CLICK: {ex.Message}\n{ex.StackTrace}");

                ShowError($"Error al conectar: {ex.Message}");

                MessageBox.Show(
                    $"Error al conectar con el servidor:\n{ex.Message}\n\nDetalles técnicos:\n{ex.StackTrace}",
                    "Error de Conexión",
                    MessageBoxButton.OK,
                    MessageBoxImage.Error
                );

                btnConnect.IsEnabled = true;
                btnConnect.Content = "Conectar";
            }
        }

        private void ShowError(string message)
        {
            try
            {
                txtError.Text = message;
                txtError.Visibility = Visibility.Visible;
                Console.WriteLine(message);
                Debug.WriteLine(message);
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Error al mostrar mensaje: {ex.Message}");
            }
        }
    }
}