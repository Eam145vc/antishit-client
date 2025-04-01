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
        // Lista de nombres de procesos que representan el juego
        private readonly List<string> _gameProcessNames = new List<string>
        {
            "ModernWarfare", "BlackOpsColdWar", "Warzone", "Vanguard", "MW2"
        };

        public void Initialize()
        {
            // Opcional: configurar monitoreo de inicio/fin de procesos
        }

        public bool IsGameRunning()
        {
            try
            {
                Process[] processes = Process.GetProcesses();

                foreach (Process process in processes)
                {
                    try
                    {
                        if (_gameProcessNames.Any(name =>
                            process.ProcessName.IndexOf(name, StringComparison.OrdinalIgnoreCase) >= 0))
                        {
                            return true;
                        }
                    }
                    catch
                    {
                        // Ignorar errores al acceder a un proceso específico
                    }
                }

                return false;
            }
            catch
            {
                return false;
            }
        }

        public List<ProcessInfo> GetRunningProcesses()
        {
            List<ProcessInfo> result = new List<ProcessInfo>();

            try
            {
                var searcher = new ManagementObjectSearcher(
                    "SELECT * FROM Win32_Process");

                foreach (ManagementObject process in searcher.Get())
                {
                    try
                    {
                        int pid = Convert.ToInt32(process["ProcessId"]);
                        string name = process["Name"]?.ToString() ?? "";
                        string execPath = process["ExecutablePath"]?.ToString() ?? "";
                        string commandLine = process["CommandLine"]?.ToString() ?? "";

                        ProcessInfo processInfo = new ProcessInfo
                        {
                            Name = name,
                            Pid = pid,
                            FilePath = execPath,
                            CommandLine = commandLine
                        };

                        // Obtener hash del archivo ejecutable si está disponible
                        if (!string.IsNullOrEmpty(execPath) && File.Exists(execPath))
                        {
                            try
                            {
                                processInfo.FileHash = CalculateSha256(execPath);
                                processInfo.FileVersion = FileVersionInfo.GetVersionInfo(execPath).FileVersion;

                                // Verificar firma del archivo (simplificado)
                                processInfo.IsSigned = IsFileSigned(execPath);
                            }
                            catch
                            {
                                // Ignorar errores al acceder al archivo
                            }
                        }

                        // Obtener tiempo de inicio
                        try
                        {
                            using (Process proc = Process.GetProcessById(pid))
                            {
                                processInfo.StartTime = proc.StartTime.ToString("yyyy-MM-dd HH:mm:ss");
                                processInfo.MemoryUsage = proc.WorkingSet64;
                            }
                        }
                        catch
                        {
                            // Ignorar errores al obtener info adicional
                        }

                        result.Add(processInfo);
                    }
                    catch
                    {
                        // Ignorar errores en procesos individuales
                    }
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error obteniendo procesos: {ex.Message}");
            }

            return result;
        }

        private string CalculateSha256(string filePath)
        {
            using (SHA256 sha256 = SHA256.Create())
            {
                using (FileStream stream = File.OpenRead(filePath))
                {
                    byte[] hash = sha256.ComputeHash(stream);
                    return BitConverter.ToString(hash).Replace("-", "").ToLowerInvariant();
                }
            }
        }

        private bool IsFileSigned(string filePath)
        {
            try
            {
                // Simplificado: en una implementación real se usaría WinVerifyTrust
                FileVersionInfo versionInfo = FileVersionInfo.GetVersionInfo(filePath);
                return !string.IsNullOrEmpty(versionInfo.CompanyName);
            }
            catch
            {
                return false;
            }
        }
    }
}