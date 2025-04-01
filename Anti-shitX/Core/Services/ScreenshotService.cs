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

        public ScreenshotService(ApiService apiService)
        {
            _apiService = apiService;
        }

        public async Task<bool> CaptureAndSendScreenshot(string activisionId, int channelId)
        {
            try
            {
                string base64Screenshot = CaptureScreenshotAsBase64();

                if (string.IsNullOrEmpty(base64Screenshot))
                    return false;

                bool result = await _apiService.SendScreenshot(activisionId, channelId, base64Screenshot);

                if (result)
                {
                    ScreenshotTaken?.Invoke(this, "Screenshot enviado correctamente");
                }

                return result;
            }
            catch (Exception ex)
            {
                ScreenshotTaken?.Invoke(this, $"Error al capturar pantalla: {ex.Message}");
                return false;
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
            catch
            {
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

                    using (MemoryStream ms = new MemoryStream())
                    {
                        // Usar calidad más baja, 50%
                        EncoderParameters encoderParams = new EncoderParameters(1);
                        encoderParams.Param[0] = new EncoderParameter(Encoder.Quality, 50L);

                        ImageCodecInfo jpegEncoder = GetEncoderInfo("image/jpeg");
                        bmp.Save(ms, jpegEncoder, encoderParams);

                        return Convert.ToBase64String(ms.ToArray());
                    }
                }
            }
            catch
            {
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