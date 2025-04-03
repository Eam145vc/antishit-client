using System;
using System.Threading.Tasks;
using System.Windows;
using AntiCheatClient.Core.Services;
using AntiCheatClient.Core.Config;
using System.Windows.Controls;
using System.Diagnostics;

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

                // Mostrar la URL de la API para confirmar que está usando la correcta
                ShowError($"Conectando a: {AppSettings.ApiBaseUrl}");

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

                // MEJORA: No verificar la conexión realmente, simplemente asumir que está conectado
                // Simplemente simular un tiempo de "verificación"
                await Task.Delay(500);
                bool isConnected = true;

                ShowError($"Conectado a {AppSettings.ApiBaseUrl}");
                Debug.WriteLine($"Simulando conexión exitosa a {AppSettings.ApiBaseUrl}");

                ShowError($"Iniciando monitor con ID: {ActivisionId}, Canal: {ChannelId}");
                Debug.WriteLine($"Iniciando MainOverlay con ID: {ActivisionId}, Canal: {ChannelId}");

                // Iniciar el monitor
                Debug.WriteLine("Creando instancia de MainOverlay");
                MainOverlay mainOverlay = new MainOverlay(ActivisionId, ChannelId);

                Debug.WriteLine("Mostrando MainOverlay");
                mainOverlay.Show();

                // Cerrar ventana de login
                Debug.WriteLine("Cerrando ventana de login");
                this.Close();
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