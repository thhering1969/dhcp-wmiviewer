// TestDefaultComputerOU.cs
// Einfache Konsolenanwendung zum Testen der Standard-Computer-OU Funktionalität

using System;
using System.Threading.Tasks;

namespace DhcpWmiViewer
{
    /// <summary>
    /// Test-Klasse für die Standard-Computer-OU Funktionalität.
    /// </summary>
    public static class TestDefaultComputerOU
    {
        /// <summary>
        /// Testet die Ermittlung der Standard-Computer-OU.
        /// </summary>
        public static async Task RunTest(string domainController = "")
        {
            Console.WriteLine("=== TEST: STANDARD-COMPUTER-OU ERMITTLUNG ===");
            Console.WriteLine();

            // Test 1: DirectoryServices-basierte Methode
            Console.WriteLine("Test 1: DirectoryServices-basierte Methode");
            Console.WriteLine("--------------------------------------------");
            
            try
            {
                var defaultOU = ADDiscovery.GetDefaultComputerOU(domainController);
                if (!string.IsNullOrEmpty(defaultOU))
                {
                    Console.WriteLine($"✓ Standard-Computer-OU gefunden: {defaultOU}");
                    
                    // Detaillierte Informationen laden
                    var ouInfo = ADDiscovery.GetDefaultComputerOUInfo(domainController);
                    if (ouInfo.IsConfigured && !ouInfo.HasError)
                    {
                        Console.WriteLine($"  Name: {ouInfo.Name}");
                        Console.WriteLine($"  Typ: {ouInfo.Type}");
                        Console.WriteLine($"  Beschreibung: {(string.IsNullOrEmpty(ouInfo.Description) ? "(keine)" : ouInfo.Description)}");
                        Console.WriteLine($"  Verwaltet von: {(string.IsNullOrEmpty(ouInfo.ManagedBy) ? "(nicht verwaltet)" : ouInfo.ManagedBy)}");
                        Console.WriteLine($"  Anzahl Computer: {ouInfo.ComputerCount}");
                    }
                    else if (ouInfo.HasError)
                    {
                        Console.WriteLine($"  ⚠ Fehler beim Laden der Details: {ouInfo.ErrorMessage}");
                    }
                }
                else
                {
                    Console.WriteLine("✗ Keine Standard-Computer-OU gefunden");
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"✗ Fehler: {ex.Message}");
            }

            Console.WriteLine();

            // Test 2: PowerShell-basierte Methode
            Console.WriteLine("Test 2: PowerShell-basierte Methode");
            Console.WriteLine("-----------------------------------");
            
            try
            {
                var psResults = await ADPowerShellExecutor.GetDefaultComputerOUAsync(domainController);
                
                if (psResults != null && psResults.Count > 0)
                {
                    var result = psResults[0];
                    var name = result.Properties["Name"]?.Value?.ToString() ?? "";
                    var dn = result.Properties["DistinguishedName"]?.Value?.ToString() ?? "";
                    var type = result.Properties["Type"]?.Value?.ToString() ?? "";
                    var description = result.Properties["Description"]?.Value?.ToString() ?? "";
                    var managedBy = result.Properties["ManagedBy"]?.Value?.ToString() ?? "";
                    var computerCount = result.Properties["ComputerCount"]?.Value?.ToString() ?? "0";
                    var isConfigured = result.Properties["IsConfigured"]?.Value?.ToString() ?? "false";
                    var errorMessage = result.Properties["ErrorMessage"]?.Value?.ToString() ?? "";
                    var domainName = result.Properties["DomainName"]?.Value?.ToString() ?? "";

                    if (isConfigured.Equals("True", StringComparison.OrdinalIgnoreCase) && string.IsNullOrEmpty(errorMessage))
                    {
                        Console.WriteLine($"✓ Standard-Computer-OU gefunden: {dn}");
                        Console.WriteLine($"  Name: {name}");
                        Console.WriteLine($"  Typ: {type}");
                        Console.WriteLine($"  Domäne: {domainName}");
                        Console.WriteLine($"  Beschreibung: {(string.IsNullOrEmpty(description) ? "(keine)" : description)}");
                        Console.WriteLine($"  Verwaltet von: {(string.IsNullOrEmpty(managedBy) ? "(nicht verwaltet)" : managedBy)}");
                        Console.WriteLine($"  Anzahl Computer: {computerCount}");
                    }
                    else
                    {
                        Console.WriteLine($"✗ PowerShell-Fehler: {errorMessage}");
                    }
                }
                else
                {
                    Console.WriteLine("✗ Keine PowerShell-Ergebnisse erhalten");
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"✗ PowerShell-Fehler: {ex.Message}");
            }

            Console.WriteLine();

            // Test 3: Vergleich mit Domain Controller Discovery
            Console.WriteLine("Test 3: Domain Controller Information");
            Console.WriteLine("------------------------------------");
            
            try
            {
                var dcs = ADDiscovery.DiscoverDomainControllersInAD();
                Console.WriteLine($"Gefundene Domain Controller: {dcs.Count}");
                foreach (var dc in dcs)
                {
                    Console.WriteLine($"  - {dc}");
                }

                var isLocalDC = ADDiscovery.CheckLocalDomainControllerServiceRunning();
                Console.WriteLine($"Lokaler Host ist DC: {(isLocalDC ? "Ja" : "Nein")}");

                if (dcs.Count > 0)
                {
                    var localInList = ADDiscovery.LocalHostAppearsInDiscovery(dcs);
                    Console.WriteLine($"Lokaler Host in DC-Liste: {(localInList ? "Ja" : "Nein")}");
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"✗ DC-Discovery-Fehler: {ex.Message}");
            }

            Console.WriteLine();
            Console.WriteLine("=== TEST ABGESCHLOSSEN ===");
        }

        /// <summary>
        /// Zeigt Verwendungshinweise an.
        /// </summary>
        public static void ShowUsage()
        {
            Console.WriteLine("=== VERWENDUNG DER STANDARD-COMPUTER-OU FUNKTIONALITÄT ===");
            Console.WriteLine();
            Console.WriteLine("Die Standard-Computer-OU ist der Container, in den neue Computer-Objekte");
            Console.WriteLine("standardmäßig aufgenommen werden, wenn sie der Domäne beitreten.");
            Console.WriteLine();
            Console.WriteLine("Typische Szenarien:");
            Console.WriteLine("• Standard-Installation: CN=Computers,DC=domain,DC=com");
            Console.WriteLine("• Small Business Server: Oft angepasste OU");
            Console.WriteLine("• Enterprise-Umgebungen: Meist angepasste OUs");
            Console.WriteLine();
            Console.WriteLine("Verwendung im Code:");
            Console.WriteLine();
            Console.WriteLine("// DirectoryServices-Methode:");
            Console.WriteLine("var defaultOU = ADDiscovery.GetDefaultComputerOU();");
            Console.WriteLine("var ouInfo = ADDiscovery.GetDefaultComputerOUInfo();");
            Console.WriteLine();
            Console.WriteLine("// PowerShell-Methode:");
            Console.WriteLine("var results = await ADPowerShellExecutor.GetDefaultComputerOUAsync(dc);");
            Console.WriteLine();
            Console.WriteLine("Die Funktionen arbeiten sowohl lokal (auf DCs) als auch remote.");
        }
    }
}