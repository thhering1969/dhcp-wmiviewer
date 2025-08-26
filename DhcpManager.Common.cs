// DhcpManager.Common.cs
using System;
using System.Management.Automation;

namespace DhcpWmiViewer
{
    public static partial class DhcpManager
    {
        // Zeigt vor jedem PowerShell-Aufruf ein Terminal-Style-Fenster mit dem Kommando an.
        // Wenn false, wird kein Fenster angezeigt und Befehle werden normal ausgeführt.
        public static bool ShowCommandPreview { get; set; } = true;

        // Hier können gemeinsame Hilfsmethoden/Konstanten ergänzt werden,
        // die von mehreren Teil-Dateien verwendet werden sollen.
    }
}
