using System;
using System.Drawing;
using System.Drawing.Imaging;
using System.IO;
using System.Threading.Tasks;
using System.Windows.Forms;
using AntiCheatClient.Core.Config;
using System.Diagnostics;

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
                Debug.WriteLine($"[ScreenshotService] Iniciando captura para {activisionId}, canal {channelId}");

                string base64Screenshot = CaptureScreenshotAsBase64();

                if (string.IsNullOrEmpty(base64Screenshot))
                {
                    Debug.WriteLine("[ScreenshotService] Error: Captura vacía o fallida");
                    ScreenshotTaken?.Invoke(this, "Error: No se pudo capturar la pantalla");
                    return false;
                }

                Debug.WriteLine($"[ScreenshotService] Captura exitosa, longitud base64: {base64Screenshot.Length}");

                // Intentar enviar al servidor hasta 3 veces
                bool sent = false;
                Exception lastException = null;

                for (int attempt = 1; attempt <= 3 && !sent; attempt++)
                {
                    try
                    {
                        Debug.WriteLine($"[ScreenshotService] Intento {attempt} de enviar captura al servidor");
                        bool result = await _apiService.SendScreenshot(activisionId, channelId, base64Screenshot);

                        if (result)
                        {
                            Debug.WriteLine("[ScreenshotService] Captura enviada exitosamente");
                            ScreenshotTaken?.Invoke(this, "Screenshot enviado correctamente");
                            return true;
                        }
                        else
                        {
                            Debug.WriteLine($"[ScreenshotService] Error en intento {attempt}: Respuesta fallida del servidor");
                            await Task.Delay(1000 * attempt); // Esperar más tiempo en cada reintento
                        }
                    }
                    catch (Exception ex)
                    {
                        lastException = ex;
                        Debug.WriteLine($"[ScreenshotService] Excepción en intento {attempt}: {ex.Message}");
                        await Task.Delay(1000 * attempt);
                    }
                }

                // Si llegamos aquí, fallaron todos los intentos
                string errorMessage = lastException != null
                    ? $"Error al enviar captura: {lastException.Message}"
                    : "Error al enviar captura: el servidor rechazó la solicitud";

                Debug.WriteLine($"[ScreenshotService] {errorMessage}");
                ScreenshotTaken?.Invoke(this, errorMessage);

                // Guardar localmente en caso de fallo
                try
                {
                    string tempPath = Path.Combine(
                        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                        "AntiCheatClient",
                        "FailedScreenshots"
                    );

                    if (!Directory.Exists(tempPath))
                    {
                        Directory.CreateDirectory(tempPath);
                    }

                    string filename = $"screenshot_{activisionId}_{DateTime.Now:yyyyMMdd_HHmmss}.jpg";
                    string fullPath = Path.Combine(tempPath, filename);

                    Debug.WriteLine($"[ScreenshotService] Guardando captura fallida en: {fullPath}");

                    // Convertir base64 a archivo
                    byte[] imageBytes = Convert.FromBase64String(base64Screenshot.Split(',')[1]);
                    File.WriteAllBytes(fullPath, imageBytes);

                    ScreenshotTaken?.Invoke(this, $"Captura guardada localmente en: {fullPath}");
                }
                catch (Exception saveEx)
                {
                    Debug.WriteLine($"[ScreenshotService] Error al guardar captura local: {saveEx.Message}");
                }

                return false;
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[ScreenshotService] Error general en CaptureAndSendScreenshot: {ex.Message}");
                ScreenshotTaken?.Invoke(this, $"Error al capturar pantalla: {ex.Message}");
                return false;
            }
        }

        private string CaptureScreenshotAsBase64()
        {
            try
            {
                Debug.WriteLine("[ScreenshotService] Capturando pantalla...");

                // Determinar el tamaño total de todas las pantallas
                System.Drawing.Rectangle totalSize = GetTotalScreenBounds();

                Debug.WriteLine($"[ScreenshotService] Tamaño de captura: {totalSize.Width}x{totalSize.Height}");

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
                        Debug.WriteLine($"[ScreenshotService] Tamaño de imagen: {bytes.Length / 1024} KB");

                        // Verificar tamaño máximo
                        if (bytes.Length > AppSettings.MaxScreenshotSize)
                        {
                            Debug.WriteLine("[ScreenshotService] Imagen demasiado grande, reduciendo calidad");
                            return CaptureScreenshotWithReducedQuality();
                        }

                        string base64 = Convert.ToBase64String(bytes);
                        return "data:image/jpeg;base64," + base64; // Formato correcto para API
                    }
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[ScreenshotService] Error al capturar pantalla: {ex.Message}");
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

                        Debug.WriteLine($"[ScreenshotService] Tamaño de imagen reducida: {ms.Length / 1024} KB");

                        return "data:image/jpeg;base64," + Convert.ToBase64String(ms.ToArray());
                    }
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[ScreenshotService] Error en captura con calidad reducida: {ex.Message}");
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
