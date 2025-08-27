// ConvertLeaseToReservationDialog.cs
// Repository: https://github.com/thhering1969/kurzzeit-dhcp-wmiviewer.git
// Branch:     fix/contextmenu-direct-call
// COMPLETE FILE - no constructor here to avoid duplicate-ctor errors.
// This partial contains only helper methods and intentionally does not
// declare a constructor. Ensure exactly one constructor exists across
// all partials (usually the ctor lives in the main .cs file or a single partial).

using System;
using System.Windows.Forms;
using System.Reflection;

namespace DhcpWmiViewer
{
    public partial class ConvertLeaseToReservationDialog : Form
    {
        // This partial intentionally does NOT declare a constructor.
        // Keep only helper/getter/setter methods here to avoid duplicate member errors.

        /// <summary>
        /// Safely set a prefetched description into the dialog if a writable control exists.
        /// Callers should prefer to pass description via ctor if available; this is a fallback.
        /// </summary>
        public void SetPrefetchedDescription(string description)
        {
            try
            {
                // Prefer a public property if the designer defines one
                var pi = this.GetType().GetProperty("Description", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
                if (pi != null && pi.CanWrite)
                {
                    pi.SetValue(this, description ?? string.Empty);
                    return;
                }

                // Otherwise try to find known TextBox names and set text
                var tb = FindControl<TextBox>("txtDescription", "txtDesc", "txtNotes");
                if (tb != null)
                {
                    tb.Text = description ?? string.Empty;
                }
            }
            catch
            {
                // defensive: do not throw from UI helper
            }
        }

        /// <summary>
        /// Robust getter: tries property via reflection (Designer may expose) otherwise reads TextBox.
        /// </summary>
        private string ReadValueOrControl(string propertyName, params string[] textBoxCandidates)
        {
            try
            {
                var pi = this.GetType().GetProperty(propertyName, BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
                if (pi != null && pi.GetMethod != null)
                {
                    var val = pi.GetValue(this);
                    if (val != null) return val.ToString() ?? string.Empty;
                }

                var tb = FindControl<TextBox>(textBoxCandidates);
                if (tb != null) return tb.Text ?? string.Empty;
            }
            catch { /* swallow */ }

            return string.Empty;
        }

        // Public helpers to be used by callers instead of directly accessing properties that may or may not exist.
        public string GetIpAddress() => ReadValueOrControl("IpAddress", "txtIp", "txtIPAddress", "txtAddr", "txtIpAddress");
        public string GetClientId() => ReadValueOrControl("ClientId", "txtClientId", "txtClientID", "txtClient");
        public string GetNameFallback() => ReadValueOrControl("Name", "txtName", "txtHostName", "txtHostname");
        public string GetDescriptionFallback() => ReadValueOrControl("Description", "txtDescription", "txtDesc", "txtNotes");

        /// <summary>
        /// Finds a control of type T by a list of candidate names (recursive search).
        /// </summary>
        private T? FindControl<T>(params string[] names) where T : Control
        {
            try
            {
                foreach (var name in names)
                {
                    if (string.IsNullOrWhiteSpace(name)) continue;
                    try
                    {
                        var found = this.Controls.Find(name, true);
                        if (found != null && found.Length > 0 && found[0] is T tb) return tb;
                    }
                    catch { /* ignore individual find errors */ }
                }
            }
            catch { /* swallow */ }
            return null;
        }
    }
}
