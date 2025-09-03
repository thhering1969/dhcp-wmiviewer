// PowerShellInitializer.cs
using System;
using System.IO;
using System.Management.Automation;
using System.Management.Automation.Runspaces;
using System.Reflection;

namespace DhcpWmiViewer
{
    /// <summary>
    /// Initialisiert PowerShell für Single-File Deployments
    /// </summary>
    internal static class PowerShellInitializer
    {
        private static bool _isInitialized = false;
        private static readonly object _lockObject = new object();

        /// <summary>
        /// Initialisiert PowerShell für Single-File Deployment
        /// </summary>
        public static void Initialize()
        {
            if (_isInitialized) return;

            lock (_lockObject)
            {
                if (_isInitialized) return;

                try
                {
                    // Einfacher Test ob PowerShell funktioniert
                    using (var ps = PowerShell.Create())
                    {
                        ps.AddScript("$PSVersionTable");
                        var results = ps.Invoke();
                        
                        if (ps.HadErrors)
                        {
                            var errors = ps.Streams.Error;
                            var errorMsg = errors.Count > 0 ? errors[0].ToString() : "Unknown PowerShell error";
                            throw new InvalidOperationException($"PowerShell initialization test failed: {errorMsg}");
                        }
                    }
                    
                    _isInitialized = true;
                }
                catch (Exception ex)
                {
                    System.Diagnostics.Debug.WriteLine($"PowerShell initialization failed: {ex}");
                    throw new InvalidOperationException("PowerShell could not be initialized for Single-File deployment", ex);
                }
            }
        }

        /// <summary>
        /// Erstellt eine neue PowerShell-Instanz mit korrekter Konfiguration
        /// </summary>
        public static PowerShell CreatePowerShell()
        {
            try
            {
                Initialize();
                return PowerShell.Create();
            }
            catch (Exception ex)
            {
                // Fallback: Versuche direkt ohne Initialisierung
                System.Diagnostics.Debug.WriteLine($"PowerShell CreatePowerShell failed, using fallback: {ex}");
                try
                {
                    return PowerShell.Create();
                }
                catch (Exception fallbackEx)
                {
                    throw new InvalidOperationException($"PowerShell creation failed. Original: {ex.Message}, Fallback: {fallbackEx.Message}", ex);
                }
            }
        }
    }
}