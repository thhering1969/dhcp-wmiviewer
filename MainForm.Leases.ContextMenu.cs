// MainForm.Leases.ContextMenu.cs
// Repo: https://github.com/thhering1969/kurzzeit-dhcp-wmiviewer.git
// Branch: fix/contextmenu-direct-call
//
// THIS FILE IS INTENTIONALLY MINIMAL.
// Implementations for the leases context menu event handlers have been consolidated
// into MainForm.ContextMenus.cs to avoid duplicate member definitions.
// Keep only non-conflicting helper code here (or leave empty).

using System;
using System.Windows.Forms;

namespace DhcpWmiViewer
{
    // COMPLETE FILE
    public partial class MainForm : Form
    {
        // Intentionally empty: Do NOT keep DgvLeases_CellMouseDown or ContextMenuLeases_Opening
        // implementations here if they exist in MainForm.ContextMenus.cs.
        //
        // Provide a convenience method to ensure menu items (if other partials call it).
        public void EnsureLeasesContextMenuInitialized()
        {
            try
            {
                try
                {
                    if (this.contextMenuLeases != null)
                    {
                        // Ensure items exist and are wired (delegates to ContextMenus partial)
                        var cms = this.contextMenuLeases;
                        // ensure items via the central partial method (if accessible)
                        var mi = this.GetType().GetMethod("EnsureLeasesMenuItems", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic);
                        if (mi != null) mi.Invoke(this, new object[] { cms });
                    }
                }
                catch { /* ignore */ }
            }
            catch { /* swallow */ }
        }
    }
}
