// DhcpManager.cs
// Minimal placeholder: stellt die DhcpManager-Klasse als partial bereit.
// Implementierungen sollten in separaten partial-Dateien (z.B. DhcpManager.Query.cs usw.) liegen.

using System;

namespace DhcpWmiViewer
{
    /// <summary>
    /// Minimaler partial-Container für die DhcpManager-Methoden.
    /// Ziel: diese Datei kann im Projekt bleiben, ohne Methoden-/Feld-Duplikate zu erzeugen,
    /// während die eigentliche Implementierung in mehreren Dateien aufgeteilt wird:
    ///   - DhcpManager.Common.cs
    ///   - DhcpManager.Query.cs
    ///   - DhcpManager.Create.cs
    ///   - DhcpManager.ChangeDelete.cs
    ///   - DhcpManager.Update.cs
    ///
    /// Hinweise:
    /// - Entferne aus dieser Datei bitte keine using-Direktiven, die von Deinen anderen partial-Dateien benötigt werden.
    /// - Falls du in den neuen partial-Dateien das Property ShowCommandPreview platzierst, darf es hier nicht erneut definiert werden.
    /// </summary>
    public static partial class DhcpManager
    {
        // Intentionally left blank.
        // Put shared constants/helpers here only if they must be visible from *all* partial files
        // and you prefer to keep them here. Otherwise keep this file empty (no duplicates).
    }
}
