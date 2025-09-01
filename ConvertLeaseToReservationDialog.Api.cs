// ConvertLeaseToReservationDialog.Api.cs
// Diese Partial-Datei enthält **keine** public API-Member (Konstruktoren, InitFields, GetReservationValues)
// — diese sind ausschließlich in ConvertLeaseToReservationDialog.cs definiert, um Duplikate zu vermeiden.

using System;
using System.ComponentModel;
using System.Windows.Forms;
using System.Data;
using System.Threading.Tasks;
using System.Linq;
using System.Reflection;

namespace DhcpWmiViewer
{
    public partial class ConvertLeaseToReservationDialog : Form
    {
        // Helper: Versucht best-effort, das embedded IpPicker-Control zu finden (reflektierend + Controls.Find).
        // Diese Methode hat einen anderen Namen als InitFields und verursacht keine Duplikate.
        private IpPicker? FindIpPickerControl()
        {
            try
            {
                // First try: private field named "ipPicker"
                var fi = this.GetType().GetField("ipPicker", BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public);
                if (fi != null)
                {
                    if (fi.GetValue(this) is IpPicker ipcFromField) return ipcFromField;
                }

                // Fallback: search in Controls tree
                var found = this.Controls.Find("ipPicker", true).FirstOrDefault();
                if (found is IpPicker ipc) return ipc;
            }
            catch
            {
                // swallow - best-effort helper
            }

            return null;
        }

        // Platz für weitere Hilfsmethoden, die keine bereits in anderen Partials vorhandenen Member duplizieren.
    }
}
