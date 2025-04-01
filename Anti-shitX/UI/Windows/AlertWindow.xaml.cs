using System.Windows;

namespace AntiCheatClient.UI.Windows
{
    public partial class AlertWindow : Window
    {
        public bool Result { get; private set; } = false;

        public AlertWindow(string title, string message)
        {
            InitializeComponent();
            txtTitle.Text = title;
            txtMessage.Text = message;
        }

        private void BtnYes_Click(object sender, RoutedEventArgs e)
        {
            Result = true;
            this.Close();
        }

        private void BtnNo_Click(object sender, RoutedEventArgs e)
        {
            Result = false;
            this.Close();
        }

        public static bool Show(string title, string message)
        {
            AlertWindow window = new AlertWindow(title, message);
            window.ShowDialog();
            return window.Result;
        }
    }
}