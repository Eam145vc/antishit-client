using System;
using System.Net.Http;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Newtonsoft.Json;
using AntiCheatClient.Core.Models;
using AntiCheatClient.Core.Config;
using System.Diagnostics;
using System.Windows;

namespace AntiCheatClient.Core.Services
{
    public class ApiService
    {
        private readonly HttpClient _httpClient;
        private readonly string _baseUrl;
        private bool _isConnected;
        private string _lastErrorMessage = "";

        public bool IsConnected => _isConnected;
        public string LastErrorMessage => _lastErrorMessage;
        public event EventHandler<bool> ConnectionStatusChanged;

        public ApiService(string baseUrl)
        {
            _baseUrl = baseUrl;

            var handler = new HttpClientHandler
            {
                // For debugging, accept any certificate
                ServerCertificateCustomValidationCallback = (message, cert, chain, errors) => true
            };

            _httpClient = new HttpClient(handler);
            _httpClient.Timeout = TimeSpan.FromSeconds(AppSettings.ApiTimeoutSeconds);

            Debug.WriteLine($"ApiService initialized with URL: {_baseUrl}");
            Console.WriteLine($"ApiService initialized with URL: {_baseUrl}");

            // Don't assume we're connected until we check
            _isConnected = false;
        }

        public async Task<bool> CheckConnection()
        {
            int attempts = 0;
            int maxAttempts = AppSettings.ConnectionRetries;

            while (attempts < maxAttempts)
            {
                attempts++;
                try
                {
                    _lastErrorMessage = "";
                    string endpoint = $"https://antishit-server2-0.onrender.com/";

                    Debug.WriteLine($"Checking connection to {endpoint} (attempt {attempts}/{maxAttempts})");
                    Console.WriteLine($"Checking connection to {endpoint} (attempt {attempts}/{maxAttempts})");

                    // Create a custom message for more control
                    var request = new HttpRequestMessage(HttpMethod.Get, endpoint);

                    // Add headers for debugging
                    request.Headers.Add("User-Agent", "AntiCheatClient/1.0");
                    request.Headers.Add("Accept", "application/json");

                    var cancellationToken = new CancellationTokenSource(TimeSpan.FromSeconds(AppSettings.ApiTimeoutSeconds)).Token;

                    // Execute the request
                    var response = await _httpClient.SendAsync(request, cancellationToken);

                    // Capture all relevant details
                    string statusDetail = $"HTTP Response: {(int)response.StatusCode} {response.ReasonPhrase}";
                    Debug.WriteLine(statusDetail);
                    Console.WriteLine(statusDetail);

                    // Capture response headers for debugging
                    if (AppSettings.DebugMode)
                    {
                        Debug.WriteLine("Response headers:");
                        foreach (var header in response.Headers)
                        {
                            Debug.WriteLine($"  {header.Key}: {string.Join(", ", header.Value)}");
                        }
                    }

                    string responseContent = await response.Content.ReadAsStringAsync();
                    bool newStatus = response.IsSuccessStatusCode;

                    if (!newStatus)
                    {
                        _lastErrorMessage = $"HTTP Error {(int)response.StatusCode}: {response.ReasonPhrase}";

                        // If not the last attempt, wait before retrying
                        if (attempts < maxAttempts)
                        {
                            Debug.WriteLine($"Waiting 1 second before retry {attempts + 1}...");
                            await Task.Delay(1000);
                            continue;
                        }

                        Debug.WriteLine($"Connection error: {_lastErrorMessage}");
                        Debug.WriteLine($"Response content: {responseContent}");

                        // Show detailed popup
                        if (AppSettings.ShowDetailedErrors)
                        {
                            await Task.Run(() => {
                                MessageBox.Show(
                                    $"Error connecting to server: {_lastErrorMessage}\n\nURL: {endpoint}\n\nResponse: {responseContent}",
                                    "Connection Error",
                                    MessageBoxButton.OK,
                                    MessageBoxImage.Error
                                );
                            });
                        }
                    }
                    else
                    {
                        Debug.WriteLine($"Successful connection. Content: {responseContent}");
                        Console.WriteLine("Successfully connected to server");
                    }

                    // Notify status change only if there's an actual change
                    if (newStatus != _isConnected)
                    {
                        _isConnected = newStatus;
                        ConnectionStatusChanged?.Invoke(this, _isConnected);

                        Debug.WriteLine($"Connection status changed to: {(_isConnected ? "CONNECTED" : "DISCONNECTED")}");
                        Console.WriteLine($"Connection status changed to: {(_isConnected ? "CONNECTED" : "DISCONNECTED")}");
                    }

                    return _isConnected;
                }
                catch (TaskCanceledException)
                {
                    _lastErrorMessage = $"Timeout ({AppSettings.ApiTimeoutSeconds} seconds) connecting to server";
                    Debug.WriteLine(_lastErrorMessage);
                    Console.WriteLine(_lastErrorMessage);

                    // If not the last attempt, wait before retrying
                    if (attempts < maxAttempts)
                    {
                        Debug.WriteLine($"Waiting 1 second before retry {attempts + 1}...");
                        await Task.Delay(1000);
                        continue;
                    }
                }
                catch (HttpRequestException ex)
                {
                    // Capture specific network errors
                    string errorDetail = "";

                    if (ex.Message.Contains("NameResolutionFailure") || ex.Message.Contains("No such host"))
                    {
                        errorDetail = "DNS resolution error. Could not find the server.";
                    }
                    else if (ex.Message.Contains("ConnectFailure") || ex.Message.Contains("connection attempt failed"))
                    {
                        errorDetail = "Connection error. The server may be down or there's a network problem.";
                    }
                    else if (ex.Message.Contains("ssl") || ex.Message.Contains("SSL") || ex.Message.Contains("TLS"))
                    {
                        errorDetail = "SSL/TLS error. There's a problem with the connection security.";
                    }
                    else
                    {
                        errorDetail = ex.Message;
                    }

                    _lastErrorMessage = $"Network error: {errorDetail}";
                    Debug.WriteLine($"Error type: {ex.GetType().Name}");
                    Debug.WriteLine($"Error connecting: {ex.Message}");

                    // If not the last attempt, wait before retrying
                    if (attempts < maxAttempts)
                    {
                        Debug.WriteLine($"Waiting 1 second before retry {attempts + 1}...");
                        await Task.Delay(1000);
                        continue;
                    }

                    // Show inner exception details
                    if (ex.InnerException != null)
                    {
                        Debug.WriteLine($"Inner Exception: {ex.InnerException.Message}");
                        Console.WriteLine($"Inner Exception: {ex.InnerException.Message}");
                        _lastErrorMessage += $"\nDetail: {ex.InnerException.Message}";
                    }
                }
                catch (Exception ex)
                {
                    _lastErrorMessage = $"Unexpected error: {ex.GetType().Name} - {ex.Message}";
                    Debug.WriteLine($"Error type: {ex.GetType().Name}");
                    Debug.WriteLine($"Error connecting: {ex.Message}");
                    Debug.WriteLine($"StackTrace: {ex.StackTrace}");

                    // If not the last attempt, wait before retrying
                    if (attempts < maxAttempts)
                    {
                        Debug.WriteLine($"Waiting 1 second before retry {attempts + 1}...");
                        await Task.Delay(1000);
                        continue;
                    }

                    // Show inner exception details
                    if (ex.InnerException != null)
                    {
                        Debug.WriteLine($"Inner Exception: {ex.InnerException.Message}");
                        Console.WriteLine($"Inner Exception: {ex.InnerException.Message}");
                        _lastErrorMessage += $"\nDetail: {ex.InnerException.Message}";
                    }
                }
            }

            // If we get here after multiple attempts, notify status change
            if (_isConnected)
            {
                _isConnected = false;
                ConnectionStatusChanged?.Invoke(this, false);
                Debug.WriteLine("Connection status changed to: DISCONNECTED (after multiple attempts)");
            }

            // Show detailed popup only on the last failed attempt
            if (AppSettings.ShowDetailedErrors)
            {
                await Task.Run(() => {
                    MessageBox.Show(
                        $"Could not connect to server after {maxAttempts} attempts.\n\nURL: {_baseUrl}/health\n\nError: {_lastErrorMessage}",
                        "Connection Error",
                        MessageBoxButton.OK,
                        MessageBoxImage.Error
                    );
                });
            }

            return false;
        }

        public async Task<bool> SendMonitorData(MonitorData data)
        {
            try
            {
                // Verify required fields
                if (string.IsNullOrEmpty(data.ActivisionId) || data.ChannelId <= 0)
                {
                    _lastErrorMessage = "Error: ActivisionId and ChannelId are required";
                    Console.WriteLine(_lastErrorMessage);
                    return false;
                }

                // Create object with the exact structure expected by the server
                var requestData = new
                {
                    activisionId = data.ActivisionId,  // Note: case sensitive, must be camelCase
                    channelId = data.ChannelId,        // Note: case sensitive, must be camelCase
                    timestamp = data.Timestamp,
                    clientStartTime = data.ClientStartTime,
                    pcStartTime = data.PcStartTime,
                    isGameRunning = data.IsGameRunning,
                    processes = data.Processes,
                    usbDevices = data.UsbDevices,
                    hardwareInfo = data.HardwareInfo,  // Make sure to include this
                    systemInfo = data.SystemInfo,      // Make sure to include this
                    networkConnections = data.NetworkConnections,
                    loadedDrivers = data.LoadedDrivers
                };

                string json = JsonConvert.SerializeObject(requestData);
                if (AppSettings.DebugMode)
                {
                    Debug.WriteLine($"Sending data: {json.Substring(0, Math.Min(json.Length, 200))}..."); // Debug log (first 200 chars)
                }
                Console.WriteLine($"Sending data to endpoint {_baseUrl}/monitor");

                var content = new StringContent(json, Encoding.UTF8, "application/json");

                // Use a CancellationToken to control the timeout
                var cancellationToken = new CancellationTokenSource(TimeSpan.FromSeconds(AppSettings.ApiTimeoutSeconds)).Token;

                var response = await _httpClient.PostAsync($"{_baseUrl}/monitor", content, cancellationToken);

                if (!response.IsSuccessStatusCode)
                {
                    string responseContent = await response.Content.ReadAsStringAsync();
                    _lastErrorMessage = $"Server error: {(int)response.StatusCode} {response.ReasonPhrase}";
                    Debug.WriteLine(_lastErrorMessage);
                    Debug.WriteLine($"Error detail: {responseContent}");
                    Console.WriteLine(_lastErrorMessage);

                    // If we get a 401/403/407 error, it could be an authentication problem
                    if ((int)response.StatusCode == 401 || (int)response.StatusCode == 403 || (int)response.StatusCode == 407)
                    {
                        _lastErrorMessage += "\nPossible authentication or authorization problem with the server.";
                    }
                    // If it's a 404 error, the URL could be wrong
                    else if ((int)response.StatusCode == 404)
                    {
                        _lastErrorMessage += "\nEndpoint not found. Verify the API URL.";
                    }
                    // If it's a 5xx error, it's a server problem
                    else if ((int)response.StatusCode >= 500)
                    {
                        _lastErrorMessage += "\nInternal server error. Contact the administrator.";
                    }
                }
                else
                {
                    Debug.WriteLine("Data sent successfully");
                    Console.WriteLine("Data sent successfully");
                }

                return response.IsSuccessStatusCode;
            }
            catch (TaskCanceledException)
            {
                _lastErrorMessage = $"Timeout ({AppSettings.ApiTimeoutSeconds} seconds) sending data to server";
                Debug.WriteLine(_lastErrorMessage);
                Console.WriteLine(_lastErrorMessage);
                return false;
            }
            catch (HttpRequestException ex)
            {
                _lastErrorMessage = $"Network error sending data: {ex.Message}";
                Debug.WriteLine(_lastErrorMessage);
                Console.WriteLine(_lastErrorMessage);
                if (ex.InnerException != null)
                {
                    Debug.WriteLine($"Inner Exception: {ex.InnerException.Message}");
                }
                return false;
            }
            catch (Exception ex)
            {
                _lastErrorMessage = $"Error sending monitoring data: {ex.GetType().Name} - {ex.Message}";
                Debug.WriteLine(_lastErrorMessage);
                Console.WriteLine(_lastErrorMessage);
                if (ex.InnerException != null)
                {
                    Debug.WriteLine($"Inner Exception: {ex.InnerException.Message}");
                }
                return false;
            }
        }

        public async Task<bool> SendGameStatus(string activisionId, int channelId, bool isGameRunning)
        {
            try
            {
                // Verify required fields
                if (string.IsNullOrEmpty(activisionId) || channelId <= 0)
                {
                    _lastErrorMessage = "Error: ActivisionId and ChannelId are required";
                    Console.WriteLine(_lastErrorMessage);
                    return false;
                }

                var data = new
                {
                    activisionId, // Already camelCase
                    channelId,    // Already camelCase
                    isGameRunning // Already camelCase
                };

                string json = JsonConvert.SerializeObject(data);
                var content = new StringContent(json, Encoding.UTF8, "application/json");

                Debug.WriteLine($"Sending game status to {_baseUrl}/game-status");
                Console.WriteLine($"Sending game status to {_baseUrl}/game-status");

                // Use a CancellationToken to control the timeout
                var cancellationToken = new CancellationTokenSource(TimeSpan.FromSeconds(AppSettings.ApiTimeoutSeconds)).Token;

                var response = await _httpClient.PostAsync($"{_baseUrl}/game-status", content, cancellationToken);

                if (!response.IsSuccessStatusCode)
                {
                    string responseContent = await response.Content.ReadAsStringAsync();
                    _lastErrorMessage = $"Error sending game status: {(int)response.StatusCode} {response.ReasonPhrase}";
                    Debug.WriteLine(_lastErrorMessage);
                    Debug.WriteLine($"Detail: {responseContent}");
                    Console.WriteLine(_lastErrorMessage);
                }
                else
                {
                    Debug.WriteLine("Game status sent successfully");
                    Console.WriteLine("Game status sent successfully");
                }

                return response.IsSuccessStatusCode;
            }
            catch (TaskCanceledException)
            {
                _lastErrorMessage = $"Timeout ({AppSettings.ApiTimeoutSeconds} seconds) sending game status";
                Debug.WriteLine(_lastErrorMessage);
                Console.WriteLine(_lastErrorMessage);
                return false;
            }
            catch (Exception ex)
            {
                _lastErrorMessage = $"Error sending game status: {ex.GetType().Name} - {ex.Message}";
                Debug.WriteLine(_lastErrorMessage);
                Console.WriteLine(_lastErrorMessage);
                if (ex.InnerException != null)
                {
                    Debug.WriteLine($"Inner Exception: {ex.InnerException.Message}");
                }
                return false;
            }
        }

        public async Task<bool> SendScreenshot(string activisionId, int channelId, string screenshotBase64)
        {
            try
            {
                // Verify required fields
                if (string.IsNullOrEmpty(activisionId) || channelId <= 0 || string.IsNullOrEmpty(screenshotBase64))
                {
                    _lastErrorMessage = "Error: ActivisionId, ChannelId and screenshot are required";
                    Console.WriteLine(_lastErrorMessage);
                    return false;
                }

                var data = new
                {
                    activisionId, // Already camelCase
                    channelId,    // Already camelCase
                    timestamp = DateTime.Now,
                    screenshot = screenshotBase64
                };

                string json = JsonConvert.SerializeObject(data);
                var content = new StringContent(json, Encoding.UTF8, "application/json");

                Debug.WriteLine($"Sending screenshot to {_baseUrl}/screenshot");
                Console.WriteLine($"Sending screenshot to {_baseUrl}/screenshot");

                // Use a CancellationToken to control the timeout
                var cancellationToken = new CancellationTokenSource(TimeSpan.FromSeconds(AppSettings.ApiTimeoutSeconds * 2)).Token; // Double timeout for screenshots

                var response = await _httpClient.PostAsync($"{_baseUrl}/screenshot", content, cancellationToken);

                if (!response.IsSuccessStatusCode)
                {
                    string responseContent = await response.Content.ReadAsStringAsync();
                    _lastErrorMessage = $"Error sending screenshot: {(int)response.StatusCode} {response.ReasonPhrase}";
                    Debug.WriteLine(_lastErrorMessage);
                    Debug.WriteLine($"Detail: {responseContent}");
                    Console.WriteLine(_lastErrorMessage);
                }
                else
                {
                    Debug.WriteLine("Screenshot sent successfully");
                    Console.WriteLine("Screenshot sent successfully");
                }

                return response.IsSuccessStatusCode;
            }
            catch (TaskCanceledException)
            {
                _lastErrorMessage = $"Timeout ({AppSettings.ApiTimeoutSeconds * 2} seconds) sending screenshot";
                Debug.WriteLine(_lastErrorMessage);
                Console.WriteLine(_lastErrorMessage);
                return false;
            }
            catch (Exception ex)
            {
                _lastErrorMessage = $"Error sending screenshot: {ex.GetType().Name} - {ex.Message}";
                Debug.WriteLine(_lastErrorMessage);
                Console.WriteLine(_lastErrorMessage);
                if (ex.InnerException != null)
                {
                    Debug.WriteLine($"Inner Exception: {ex.InnerException.Message}");
                }
                return false;
            }
        }

        public async Task<bool> CheckScreenshotRequests(string activisionId, int channelId)
        {
            try
            {
                if (string.IsNullOrEmpty(activisionId) || channelId <= 0)
                {
                    return false;
                }

                Debug.WriteLine($"Checking screenshot requests for {activisionId}, channel {channelId}");

                var url = $"{_baseUrl}/screenshots/check-requests?activisionId={activisionId}&channelId={channelId}";
                var cancellationToken = new CancellationTokenSource(TimeSpan.FromSeconds(5)).Token;
                var response = await _httpClient.GetAsync(url, cancellationToken);

                if (!response.IsSuccessStatusCode)
                {
                    Debug.WriteLine($"Error checking screenshot requests: {response.StatusCode}");
                    return false;
                }

                var content = await response.Content.ReadAsStringAsync();
                var result = JsonConvert.DeserializeObject<CheckRequestResult>(content);

                Debug.WriteLine($"Screenshot request check result: {result?.hasRequest}");
                return result?.hasRequest ?? false;
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Error checking screenshot requests: {ex.Message}");
                return false;
            }
        }

        private class CheckRequestResult
        {
            public bool hasRequest { get; set; }
            public string message { get; set; }
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

                Debug.WriteLine($"Reporting error to {_baseUrl}/error");

                // Use a CancellationToken to control the timeout
                var cancellationToken = new CancellationTokenSource(TimeSpan.FromSeconds(AppSettings.ApiTimeoutSeconds)).Token;

                var response = await _httpClient.PostAsync($"{_baseUrl}/error", content, cancellationToken);

                if (!response.IsSuccessStatusCode)
                {
                    _lastErrorMessage = $"Error reporting error: {(int)response.StatusCode} {response.ReasonPhrase}";
                    Debug.WriteLine(_lastErrorMessage);
                }
                else
                {
                    Debug.WriteLine("Error reported successfully");
                }

                return response.IsSuccessStatusCode;
            }
            catch (Exception ex)
            {
                _lastErrorMessage = $"Exception reporting error: {ex.GetType().Name} - {ex.Message}";
                Debug.WriteLine(_lastErrorMessage);
                return false;
            }
        }
    }
}