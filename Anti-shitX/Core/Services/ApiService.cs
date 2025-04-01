using System;
using System.Net.Http;
using System.Text;
using System.Threading.Tasks;
using Newtonsoft.Json;
using AntiCheatClient.Core.Models;

namespace AntiCheatClient.Core.Services
{
    public class ApiService
    {
        private readonly HttpClient _httpClient;
        private readonly string _baseUrl;
        private bool _isConnected;

        public bool IsConnected => _isConnected;
        public event EventHandler<bool> ConnectionStatusChanged;

        public ApiService(string baseUrl)
        {
            _baseUrl = baseUrl;
            _httpClient = new HttpClient();
            _httpClient.Timeout = TimeSpan.FromSeconds(30);
        }

        public async Task<bool> CheckConnection()
        {
            try
            {
                var response = await _httpClient.GetAsync($"{_baseUrl}/health");
                bool newStatus = response.IsSuccessStatusCode;

                if (newStatus != _isConnected)
                {
                    _isConnected = newStatus;
                    ConnectionStatusChanged?.Invoke(this, _isConnected);
                }

                return _isConnected;
            }
            catch
            {
                if (_isConnected)
                {
                    _isConnected = false;
                    ConnectionStatusChanged?.Invoke(this, false);
                }
                return false;
            }
        }

        public async Task<bool> SendMonitorData(MonitorData data)
        {
            try
            {
                // Verificar campos obligatorios
                if (string.IsNullOrEmpty(data.ActivisionId) || data.ChannelId <= 0)
                {
                    Console.WriteLine("Error: ActivisionId y ChannelId son obligatorios");
                    return false;
                }

                // Crear un objeto anónimo con exactamente la estructura que espera el servidor
                var requestData = new
                {
                    activisionId = data.ActivisionId,  // Nota: case sensitive, debe ser camelCase
                    channelId = data.ChannelId,        // Nota: case sensitive, debe ser camelCase
                    timestamp = data.Timestamp,
                    clientStartTime = data.ClientStartTime,
                    pcStartTime = data.PcStartTime,
                    isGameRunning = data.IsGameRunning,
                    processes = data.Processes,
                    usbDevices = data.UsbDevices,
                    hardwareInfo = data.HardwareInfo,
                    systemInfo = data.SystemInfo,
                    networkConnections = data.NetworkConnections,
                    loadedDrivers = data.LoadedDrivers
                };

                string json = JsonConvert.SerializeObject(requestData);
                Console.WriteLine($"Enviando datos: {json}"); // Log para depuración

                var content = new StringContent(json, Encoding.UTF8, "application/json");
                var response = await _httpClient.PostAsync($"{_baseUrl}/monitor", content);

                if (!response.IsSuccessStatusCode)
                {
                    string responseContent = await response.Content.ReadAsStringAsync();
                    Console.WriteLine($"Error del servidor: {responseContent}"); // Log para depuración
                }

                return response.IsSuccessStatusCode;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error enviando datos de monitoreo: {ex.Message}");
                return false;
            }
        }

        public async Task<bool> SendGameStatus(string activisionId, int channelId, bool isGameRunning)
        {
            try
            {
                // Verificar campos obligatorios
                if (string.IsNullOrEmpty(activisionId) || channelId <= 0)
                {
                    Console.WriteLine("Error: ActivisionId y ChannelId son obligatorios");
                    return false;
                }

                var data = new
                {
                    activisionId, // Ya en camelCase
                    channelId,    // Ya en camelCase
                    isGameRunning // Ya en camelCase
                };

                string json = JsonConvert.SerializeObject(data);
                var content = new StringContent(json, Encoding.UTF8, "application/json");
                var response = await _httpClient.PostAsync($"{_baseUrl}/game-status", content);

                if (!response.IsSuccessStatusCode)
                {
                    string responseContent = await response.Content.ReadAsStringAsync();
                    Console.WriteLine($"Error enviando estado del juego: {responseContent}");
                }

                return response.IsSuccessStatusCode;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error enviando estado del juego: {ex.Message}");
                return false;
            }
        }

        public async Task<bool> SendScreenshot(string activisionId, int channelId, string screenshotBase64)
        {
            try
            {
                // Verificar campos obligatorios
                if (string.IsNullOrEmpty(activisionId) || channelId <= 0 || string.IsNullOrEmpty(screenshotBase64))
                {
                    Console.WriteLine("Error: ActivisionId, ChannelId y screenshot son obligatorios");
                    return false;
                }

                var data = new
                {
                    activisionId, // Ya en camelCase
                    channelId,    // Ya en camelCase
                    timestamp = DateTime.Now,
                    screenshot = screenshotBase64
                };

                string json = JsonConvert.SerializeObject(data);
                var content = new StringContent(json, Encoding.UTF8, "application/json");
                var response = await _httpClient.PostAsync($"{_baseUrl}/screenshot", content);

                if (!response.IsSuccessStatusCode)
                {
                    string responseContent = await response.Content.ReadAsStringAsync();
                    Console.WriteLine($"Error enviando screenshot: {responseContent}");
                }

                return response.IsSuccessStatusCode;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error enviando screenshot: {ex.Message}");
                return false;
            }
        }

        public async Task<bool> ReportError(string errorMessage)
        {
            try
            {
                var data = new
                {
                    error = errorMessage,
                    timestamp = DateTime.Now
                };

                string json = JsonConvert.SerializeObject(data);
                var content = new StringContent(json, Encoding.UTF8, "application/json");
                var response = await _httpClient.PostAsync($"{_baseUrl}/error", content);
                return response.IsSuccessStatusCode;
            }
            catch
            {
                return false;
            }
        }
    }
}