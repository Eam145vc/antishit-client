using System;
using System.Drawing;
using System.Drawing.Imaging;
using System.IO;
using System.Threading.Tasks;
using System.Windows.Forms;
using AntiCheatClient.Core.Config;

namespace AntiCheatClient.Core.Services
{
    public class ScreenshotService
    {
        private readonly ApiService _apiService;
        public event EventHandler<string> ScreenshotTaken;
        private bool _isCapturing = false;

        public ScreenshotService(ApiService apiService)
        {
            _apiService = apiService;

            // Suscribirse al evento de solicitud de captura del ApiService
            _apiService.ScreenshotRequested += ApiService_ScreenshotRequested;
        }

        private async void ApiService_ScreenshotRequested(object sender, ApiService.ScreenshotRequestEventArgs e)
        {
            try
            {
                // Evitar capturas simultáneas
                if (_isCapturing)
                {
                    Console.WriteLine("Ya hay una captura en proceso, ignorando solicitud");
                    return;
                }

                Console.WriteLine($"Solicitud de captura recibida de: {e.RequestedBy}");

                // Obtener información del jugador actual
                string activisionId = UI.Windows.MainOverlay.CurrentActivisionId;
                int channelId = UI.Windows.MainOverlay.CurrentChannelId;

                if (string.IsNullOrEmpty(activisionId) || channelId <= 0)
                {
                    Console.WriteLine("No se pudo obtener información del jugador para la captura");
                    return;
                }

                // Notificar que se está tomando la captura
                ScreenshotTaken?.Invoke(this, $"Tomando captura solicitada por {e.RequestedBy}...");

                // Tomar y enviar la captura automáticamente
                await CaptureAndSendScreenshot(activisionId, channelId);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error procesando solicitud de captura: {ex.Message}");
            }
        }

        public async Task<bool> CaptureAndSendScreenshot(string activisionId, int channelId)
        {
            if (_isCapturing)
            {
                ScreenshotTaken?.Invoke(this, "Ya hay una captura en proceso");
                return false;
            }

            try
            {
                _isCapturing = true;
                ScreenshotTaken?.Invoke(this, "Capturando pantalla...");

                string base64Screenshot = CaptureScreenshotAsBase64();

                if (string.IsNullOrEmpty(base64Screenshot))
                {
                    ScreenshotTaken?.Invoke(this, "Error: No se pudo capturar la pantalla");
                    return false;
                }

                ScreenshotTaken?.Invoke(this, "Enviando captura al servidor...");
                bool result = await _apiService.SendScreenshot(activisionId, channelId, base64Screenshot);

                if (result)
                {
                    ScreenshotTaken?.Invoke(this, "Screenshot enviado correctamente");
                }
                else
                {
                    ScreenshotTaken?.Invoke(this, "Error: No se pudo enviar la captura al servidor");
                }

                return result;
            }
            catch (Exception ex)
            {
                ScreenshotTaken?.Invoke(this, $"Error al capturar pantalla: {ex.Message}");
                return false;
            }
            finally
            {
                _isCapturing = false;
            }
        }

        private string CaptureScreenshotAsBase64()
        {
            try
            {
                // Determinar el tamaño total de todas las pantallas
                System.Drawing.Rectangle totalSize = GetTotalScreenBounds();

                // Crear bitmap para almacenar la captura
                using (Bitmap bmp = new Bitmap(totalSize.Width, totalSize.Height))
                {
                    // Crear un contexto gráfico
                    using (Graphics g = Graphics.FromImage(bmp))
                    {
                        // Capturar pantalla(s)
                        g.CopyFromScreen(totalSize.Left, totalSize.Top, 0, 0, totalSize.Size);
                    }

                    // Convertir a base64
                    using (MemoryStream ms = new MemoryStream())
                    {
                        // Usar compresión JPEG para reducir tamaño
                        EncoderParameters encoderParams = new EncoderParameters(1);
                        encoderParams.Param[0] = new EncoderParameter(Encoder.Quality, 85L); // 85% de calidad

                        ImageCodecInfo jpegEncoder = GetEncoderInfo("image/jpeg");
                        bmp.Save(ms, jpegEncoder, encoderParams);

                        byte[] bytes = ms.ToArray();

                        // Verificar tamaño máximo
                        if (bytes.Length > AppSettings.MaxScreenshotSize)
                        {
                            // Reducir calidad si es demasiado grande
                            return CaptureScreenshotWithReducedQuality();
                        }

                        return Convert.ToBase64String(bytes);
                    }
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error al capturar pantalla: {ex.Message}");
                return string.Empty;
            }
        }

        private string CaptureScreenshotWithReducedQuality()
        {
            try
            {
                // Similar a CaptureScreenshotAsBase64, pero con calidad más baja
                System.Drawing.Rectangle totalSize = GetTotalScreenBounds();

                using (Bitmap bmp = new Bitmap(totalSize.Width, totalSize.Height))
                {
                    using (Graphics g = Graphics.FromImage(bmp))
                    {
                        g.CopyFromScreen(totalSize.Left, totalSize.Top, 0, 0, totalSize.Size);
                    }

                    // Reducir el tamaño de la imagen a la mitad
                    using (Bitmap resizedBmp = new Bitmap(bmp, new Size(bmp.Width / 2, bmp.Height / 2)))
                    {
                        using (MemoryStream ms = new MemoryStream())
                        {
                            // Usar calidad más baja, 50%
                            EncoderParameters encoderParams = new EncoderParameters(1);
                            encoderParams.Param[0] = new EncoderParameter(Encoder.Quality, 50L);

                            ImageCodecInfo jpegEncoder = GetEncoderInfo("image/jpeg");
                            resizedBmp.Save(ms, jpegEncoder, encoderParams);

                            byte[] bytes = ms.ToArray();

                            // Si sigue siendo demasiado grande, reducir aún más
                            if (bytes.Length > AppSettings.MaxScreenshotSize)
                            {
                                // Usar un tamaño aún más pequeño y menor calidad
                                return CaptureScreenshotWithMinimumQuality();
                            }

                            return Convert.ToBase64String(bytes);
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error al capturar pantalla con calidad reducida: {ex.Message}");
                return string.Empty;
            }
        }

        private string CaptureScreenshotWithMinimumQuality()
        {
            try
            {
                // Capturar con calidad mínima
                System.Drawing.Rectangle totalSize = GetTotalScreenBounds();

                using (Bitmap bmp = new Bitmap(totalSize.Width, totalSize.Height))
                {
                    using (Graphics g = Graphics.FromImage(bmp))
                    {
                        g.CopyFromScreen(totalSize.Left, totalSize.Top, 0, 0, totalSize.Size);
                    }

                    // Reducir el tamaño de la imagen a un tercio
                    using (Bitmap resizedBmp = new Bitmap(bmp, new Size(bmp.Width / 3, bmp.Height / 3)))
                    {
                        using (MemoryStream ms = new MemoryStream())
                        {
                            // Usar calidad mínima, 30%
                            EncoderParameters encoderParams = new EncoderParameters(1);
                            encoderParams.Param[0] = new EncoderParameter(Encoder.Quality, 30L);

                            ImageCodecInfo jpegEncoder = GetEncoderInfo("image/jpeg");
                            resizedBmp.Save(ms, jpegEncoder, encoderParams);

                            return Convert.ToBase64String(ms.ToArray());
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error al capturar pantalla con calidad mínima: {ex.Message}");
                return string.Empty;
            }
        }

        private System.Drawing.Rectangle GetTotalScreenBounds()
        {
            System.Drawing.Rectangle result = new System.Drawing.Rectangle();
            foreach (Screen screen in Screen.AllScreens)
            {
                result = System.Drawing.Rectangle.Union(result, screen.Bounds);
            }
            return result;
        }

        private ImageCodecInfo GetEncoderInfo(string mimeType)
        {
            ImageCodecInfo[] codecs = ImageCodecInfo.GetImageEncoders();

            for (int i = 0; i < codecs.Length; i++)
            {
                if (codecs[i].MimeType == mimeType)
                    return codecs[i];
            }

            return null;
        }
    }
}