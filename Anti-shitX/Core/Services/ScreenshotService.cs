using System;
using System.Drawing;
using System.Drawing.Imaging;
using System.IO;
using System.Threading.Tasks;
using System.Windows.Forms;
using AntiCheatClient.Core.Config;
using System.Timers;
using System.Diagnostics;

namespace AntiCheatClient.Core.Services
{
    public class ScreenshotService
    {
        private readonly ApiService _apiService;
        private System.Timers.Timer _checkTimer;
        public event EventHandler<string> ScreenshotTaken;

        public ScreenshotService(ApiService apiService)
        {
            _apiService = apiService;

            // Initialize timer for checking screenshot requests
            _checkTimer = new System.Timers.Timer(5000); // Check every 5 seconds
            _checkTimer.Elapsed += CheckForScreenshotRequests;
            _checkTimer.AutoReset = true;
            _checkTimer.Start();
        }

        public async Task<bool> CaptureAndSendScreenshot(string activisionId, int channelId)
        {
            try
            {
                Debug.WriteLine($"Starting screenshot capture for {activisionId}, channel {channelId}");
                string base64Screenshot = CaptureScreenshotAsBase64();

                if (string.IsNullOrEmpty(base64Screenshot))
                {
                    Debug.WriteLine("Screenshot capture failed - empty result");
                    return false;
                }

                Debug.WriteLine("Screenshot captured successfully, sending to server");
                bool result = await _apiService.SendScreenshot(activisionId, channelId, base64Screenshot);

                if (result)
                {
                    Debug.WriteLine("Screenshot sent successfully");
                    ScreenshotTaken?.Invoke(this, "Screenshot enviado correctamente");
                }
                else
                {
                    Debug.WriteLine("Failed to send screenshot to server");
                    ScreenshotTaken?.Invoke(this, "Error al enviar screenshot al servidor");
                }

                return result;
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Error during screenshot capture and send: {ex.Message}");
                ScreenshotTaken?.Invoke(this, $"Error al capturar pantalla: {ex.Message}");
                return false;
            }
        }

        private async void CheckForScreenshotRequests(object sender, ElapsedEventArgs e)
        {
            try
            {
                // Prevent timer overlap if previous check is still running
                _checkTimer.Stop();

                if (UI.Windows.MainOverlay.CurrentActivisionId == null ||
                    UI.Windows.MainOverlay.CurrentChannelId <= 0)
                {
                    Debug.WriteLine("Cannot check for screenshot requests - missing player info");
                    return;
                }

                Debug.WriteLine("Checking for pending screenshot requests...");
                bool hasRequest = await _apiService.CheckScreenshotRequests(
                    UI.Windows.MainOverlay.CurrentActivisionId,
                    UI.Windows.MainOverlay.CurrentChannelId);

                if (hasRequest)
                {
                    Debug.WriteLine("Screenshot request detected! Taking screenshot...");
                    await CaptureAndSendScreenshot(
                        UI.Windows.MainOverlay.CurrentActivisionId,
                        UI.Windows.MainOverlay.CurrentChannelId);
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Error checking for screenshot requests: {ex.Message}");
            }
            finally
            {
                // Restart timer
                _checkTimer.Start();
            }
        }

        private string CaptureScreenshotAsBase64()
        {
            try
            {
                // Determine the total size of all screens
                System.Drawing.Rectangle totalSize = GetTotalScreenBounds();

                // Create bitmap to store the capture
                using (Bitmap bmp = new Bitmap(totalSize.Width, totalSize.Height))
                {
                    // Create a graphics context
                    using (Graphics g = Graphics.FromImage(bmp))
                    {
                        // Capture screen(s)
                        g.CopyFromScreen(totalSize.Left, totalSize.Top, 0, 0, totalSize.Size);
                    }

                    // Convert to base64
                    using (MemoryStream ms = new MemoryStream())
                    {
                        // Use JPEG compression to reduce size
                        EncoderParameters encoderParams = new EncoderParameters(1);
                        encoderParams.Param[0] = new EncoderParameter(Encoder.Quality, 85L); // 85% quality

                        ImageCodecInfo jpegEncoder = GetEncoderInfo("image/jpeg");
                        bmp.Save(ms, jpegEncoder, encoderParams);

                        byte[] bytes = ms.ToArray();

                        // Check maximum size
                        if (bytes.Length > AppSettings.MaxScreenshotSize)
                        {
                            // Reduce quality if too large
                            return CaptureScreenshotWithReducedQuality();
                        }

                        return Convert.ToBase64String(bytes);
                    }
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Error capturing screenshot: {ex.Message}");
                return string.Empty;
            }
        }

        private string CaptureScreenshotWithReducedQuality()
        {
            try
            {
                // Similar to CaptureScreenshotAsBase64, but with lower quality
                System.Drawing.Rectangle totalSize = GetTotalScreenBounds();

                using (Bitmap bmp = new Bitmap(totalSize.Width, totalSize.Height))
                {
                    using (Graphics g = Graphics.FromImage(bmp))
                    {
                        g.CopyFromScreen(totalSize.Left, totalSize.Top, 0, 0, totalSize.Size);
                    }

                    using (MemoryStream ms = new MemoryStream())
                    {
                        // Use lower quality, 50%
                        EncoderParameters encoderParams = new EncoderParameters(1);
                        encoderParams.Param[0] = new EncoderParameter(Encoder.Quality, 50L);

                        ImageCodecInfo jpegEncoder = GetEncoderInfo("image/jpeg");
                        bmp.Save(ms, jpegEncoder, encoderParams);

                        return Convert.ToBase64String(ms.ToArray());
                    }
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Error capturing screenshot with reduced quality: {ex.Message}");
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