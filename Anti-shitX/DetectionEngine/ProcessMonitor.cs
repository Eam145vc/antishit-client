// Path: Anti-shitX/DetectionEngine/ProcessMonitor.cs

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
        // Add version constant to track changes
        private const string VERSION = "1.2.0-20250405"; // Format: Version-YYYYMMDD

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

            // Log version to confirm we're running the updated code
            Console.WriteLine($"ProcessMonitor running version: {VERSION}");
            Debug.WriteLine($"ProcessMonitor running version: {VERSION}");

            try
            {
                // Use direct Process.GetProcesses() approach for better compatibility
                var allProcesses = Process.GetProcesses();
                Console.WriteLine($"Found {allProcesses.Length} running processes");

                foreach (var process in allProcesses)
                {
                    try
                    {
                        var processInfo = new ProcessInfo
                        {
                            Name = process.ProcessName,
                            Pid = process.Id
                        };

                        // Try to get file info (will fail for system processes)
                        try
                        {
                            // MainModule might throw exception for system processes
                            if (process.MainModule != null)
                            {
                                processInfo.FilePath = process.MainModule.FileName;

                                // Get additional file info
                                if (File.Exists(processInfo.FilePath))
                                {
                                    var fileInfo = new FileInfo(processInfo.FilePath);
                                    var versionInfo = FileVersionInfo.GetVersionInfo(processInfo.FilePath);

                                    processInfo.FileVersion = versionInfo.FileVersion ?? "N/A";
                                    processInfo.IsSigned = !string.IsNullOrEmpty(versionInfo.CompanyName);

                                    try
                                    {
                                        // Calculate file hash safely
                                        using (var fileStream = new FileStream(
                                            processInfo.FilePath,
                                            FileMode.Open,
                                            FileAccess.Read,
                                            FileShare.ReadWrite | FileShare.Delete))
                                        {
                                            using (var sha256 = SHA256.Create())
                                            {
                                                var hashBytes = sha256.ComputeHash(fileStream);
                                                processInfo.FileHash = BitConverter.ToString(hashBytes).Replace("-", "");
                                            }
                                        }
                                    }
                                    catch
                                    {
                                        processInfo.FileHash = "Error al calcular";
                                    }
                                }
                            }
                        }
                        catch (Exception ex)
                        {
                            // System process or access denied
                            processInfo.FilePath = "Acceso restringido: " + ex.Message;
                        }

                        // Get memory usage
                        try
                        {
                            processInfo.MemoryUsage = process.WorkingSet64;
                        }
                        catch
                        {
                            processInfo.MemoryUsage = 0;
                        }

                        // Get start time
                        try
                        {
                            processInfo.StartTime = process.StartTime.ToString("yyyy-MM-dd HH:mm:ss");
                        }
                        catch
                        {
                            processInfo.StartTime = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss");
                        }

                        // Try to get command line using WMI
                        try
                        {
                            using (var searcher = new ManagementObjectSearcher(
                                $"SELECT CommandLine FROM Win32_Process WHERE ProcessId = {process.Id}"))
                            {
                                foreach (var item in searcher.Get())
                                {
                                    processInfo.CommandLine = item["CommandLine"]?.ToString() ?? "N/A";
                                    break;
                                }
                            }
                        }
                        catch
                        {
                            processInfo.CommandLine = "N/A";
                        }

                        // Set defaults for any missing data
                        if (string.IsNullOrEmpty(processInfo.Name)) processInfo.Name = "Proceso " + process.Id;
                        if (string.IsNullOrEmpty(processInfo.FilePath)) processInfo.FilePath = "N/A";
                        if (string.IsNullOrEmpty(processInfo.FileHash)) processInfo.FileHash = "N/A";
                        if (string.IsNullOrEmpty(processInfo.CommandLine)) processInfo.CommandLine = "N/A";
                        if (string.IsNullOrEmpty(processInfo.FileVersion)) processInfo.FileVersion = "N/A";
                        if (string.IsNullOrEmpty(processInfo.StartTime)) processInfo.StartTime = "N/A";

                        // Add version indicator to show this is from the updated code
                        processInfo.SignatureInfo = $"Procesado por v{VERSION}";

                        result.Add(processInfo);
                    }
                    catch (Exception procEx)
                    {
                        Console.WriteLine($"Error procesando proceso {process.Id}: {procEx.Message}");

                        // Add minimal info for the failed process
                        result.Add(new ProcessInfo
                        {
                            Name = process.ProcessName ?? "Error-" + process.Id,
                            Pid = process.Id,
                            FilePath = "Error al procesar",
                            FileHash = "N/A",
                            FileVersion = "N/A",
                            IsSigned = false,
                            SignatureInfo = $"Error: {procEx.Message}",
                            MemoryUsage = 0,
                            StartTime = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss")
                        });
                    }
                }

                // Log success with count
                Console.WriteLine($"Successfully collected {result.Count} processes");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error general en GetRunningProcesses: {ex.Message}");
                Debug.WriteLine($"Error general en GetRunningProcesses: {ex.Message}");

                // Add a single error entry
                result.Add(new ProcessInfo
                {
                    Name = "ERROR en recolección",
                    Pid = -1,
                    FilePath = ex.Message,
                    FileHash = "N/A",
                    FileVersion = "N/A",
                    IsSigned = false,
                    SignatureInfo = $"Error v{VERSION}: {ex.Message}",
                    MemoryUsage = 0,
                    StartTime = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss")
                });
            }

            return result;
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