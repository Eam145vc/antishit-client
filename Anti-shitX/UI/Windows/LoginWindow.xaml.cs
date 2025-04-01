using System;
using System.Threading.Tasks;
using System.Windows;
using AntiCheatClient.Core.Services;
using AntiCheatClient.Core.Config;
using System.Windows.Controls;

namespace AntiCheatClient.UI.Windows
{
    public partial class LoginWindow : Window
    {
        private readonly ApiService _apiService;

        public string ActivisionId { get; private set; }
        public int ChannelId { get; private set; }

        public LoginWindow()
        {
            InitializeComponent();

            _apiService = new ApiService(AppSettings.ApiBaseUrl);
            cmbChannel.SelectedIndex = 0;
        }

        private async void BtnConnect_Click(object sender, RoutedEventArgs e)
        {
            // Validar entrada
            if (string.IsNullOrWhiteSpace(txtActivisionId.Text))
            {
                ShowError("Por favor ingresa tu ID de Activision.");
                return;
            }

            if (cmbChannel.SelectedItem == null)
            {
                ShowError("Por favor selecciona un canal.");
                return;
            }

            try
            {
                // Deshabilitar botón durante la conexión
                btnConnect.IsEnabled = false;
                btnConnect.Content = "Conectando...";

                // Verificar conexión con el servidor
                bool isConnected = await _apiService.CheckConnection();

                if (!isConnected)
                {
                    ShowError(Constants.Messages.ServerConnectionError);
                    btnConnect.IsEnabled = true;
                    btnConnect.Content = "Conectar";
                    return;
                }

                // Obtener valores
                ActivisionId = txtActivisionId.Text.Trim();
                ChannelId = int.Parse(((ComboBoxItem)cmbChannel.SelectedItem).Tag.ToString());

                // Iniciar el monitor
                MainOverlay mainOverlay = new MainOverlay(ActivisionId, ChannelId);
                mainOverlay.Show();

                // Cerrar ventana de login
                this.Close();
            }
            catch (Exception ex)
            {
                ShowError($"Error al conectar: {ex.Message}");
                btnConnect.IsEnabled = true;
                btnConnect.Content = "Conectar";
            }
        }

        private void ShowError(string message)
        {
            txtError.Text = message;
            txtError.Visibility = Visibility.Visible;
        }
    }
}