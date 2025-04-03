using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Management;
using System.Security.Cryptography;
using AntiCheatClient.Core.Models;

namespace AntiCheatClient.DetectionEngine
{
    public class ProcessMonitor
    {
        // Nombres de procesos de juego conocidos
        private static readonly string[] GameProcessNames = new[]
        {
            "ModernWarfare", "BlackOpsColdWar", "Warzone",
            "Vanguard", "MW2", "cod"
        };

        /// <summary>
        /// Obtiene una lista de todos los procesos en ejecución
        /// </summary>
        /// <returns>Lista de información de procesos</returns>
        public List<ProcessInfo> GetRunningProcesses()
        {
            var result = new List<ProcessInfo>();

            try
            {
                using (var searcher = new ManagementObjectSearcher("SELECT * FROM Win32_Process"))
                {
                    foreach (ManagementObject processObj in searcher.Get())
                    {
                        try
                        {
                            var processInfo = CreateProcessInfo(processObj);

                            // Verificar si es un proceso de juego conocido
                            if (!IsGameProcess(processInfo.Name))
                            {
                                processInfo.IsSigned = IsFileSigned(processInfo.FilePath);
                            }

                            result.Add(processInfo);
                        }
                        catch (Exception procEx)
                        {
                            Console.WriteLine($"Error procesando proceso: {procEx.Message}");
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error al obtener procesos: {ex.Message}");
            }

            return result;
        }

        /// <summary>
        /// Crea un objeto ProcessInfo a partir de un objeto de administración
        /// </summary>
        private ProcessInfo CreateProcessInfo(ManagementObject processObj)
        {
            var processInfo = new ProcessInfo
            {
                Pid = Convert.ToInt32(processObj["ProcessId"]),
                Name = processObj["Name"]?.ToString() ?? "Desconocido",
                FilePath = processObj["ExecutablePath"]?.ToString() ?? string.Empty,
                CommandLine = processObj["CommandLine"]?.ToString() ?? string.Empty
            };

            // Obtener información adicional del archivo
            if (!string.IsNullOrEmpty(processInfo.FilePath) && File.Exists(processInfo.FilePath))
            {
                try
                {
                    var versionInfo = FileVersionInfo.GetVersionInfo(processInfo.FilePath);
                    processInfo.FileHash = CalculateFileHash(processInfo.FilePath);
                    processInfo.FileVersion = versionInfo.FileVersion ?? "N/A";
                }
                catch
                {
                    processInfo.FileHash = "N/A";
                    processInfo.FileVersion = "N/A";
                }
            }

            // Obtener información de ejecución
            try
            {
                using (Process proc = Process.GetProcessById(processInfo.Pid))
                {
                    processInfo.StartTime = proc.StartTime.ToString("yyyy-MM-dd HH:mm:ss");
                    processInfo.MemoryUsage = proc.WorkingSet64;
                }
            }
            catch
            {
                processInfo.StartTime = "N/A";
                processInfo.MemoryUsage = 0;
            }

            return processInfo;
        }

        /// <summary>
        /// Calcula el hash SHA-256 de un archivo
        /// </summary>
        private string CalculateFileHash(string filePath)
        {
            try
            {
                using (var sha256 = SHA256.Create())
                {
                    using (var stream = File.OpenRead(filePath))
                    {
                        var hash = sha256.ComputeHash(stream);
                        return BitConverter.ToString(hash).Replace("-", "").ToLowerInvariant();
                    }
                }
            }
            catch
            {
                return "N/A";
            }
        }

        /// <summary>
        /// Verifica si un archivo está firmado digitalmente
        /// </summary>
        private bool IsFileSigned(string filePath)
        {
            try
            {
                if (string.IsNullOrEmpty(filePath) || !File.Exists(filePath))
                    return false;

                var versionInfo = FileVersionInfo.GetVersionInfo(filePath);
                return !string.IsNullOrEmpty(versionInfo.CompanyName) &&
                       (versionInfo.CompanyName.Contains("Microsoft") ||
                        versionInfo.CompanyName.Contains("Windows"));
            }
            catch
            {
                return false;
            }
        }

        /// <summary>
        /// Verifica si un nombre de proceso pertenece a un juego conocido
        /// </summary>
        private bool IsGameProcess(string processName)
        {
            return GameProcessNames.Any(game =>
                processName.IndexOf(game, StringComparison.OrdinalIgnoreCase) >= 0);
        }

        /// <summary>
        /// Verifica si alguno de los juegos está en ejecución
        /// </summary>
        public bool IsGameRunning()
        {
            try
            {
                Process[] processes = Process.GetProcesses();

                return processes.Any(process =>
                    IsGameProcess(process.ProcessName));
            }
            catch
            {
                return false;
            }
        }
    }
}