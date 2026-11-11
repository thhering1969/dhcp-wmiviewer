// WaitDialog.cs
namespace DhcpWmiViewer
{
    /// <summary>Modaler "Bitte warten"-Dialog, solange eine asynchrone Operation läuft.</summary>
    internal sealed class WaitDialog : Form
    {
        private readonly ProgressBar _bar;
        private readonly Label _label;
        private readonly string _message;

        private WaitDialog(string message)
        {
            _message = string.IsNullOrWhiteSpace(message) ? "Bitte warten…" : message;
            FormBorderStyle = FormBorderStyle.FixedDialog;
            ControlBox = false;
            StartPosition = FormStartPosition.CenterParent;
            MinimizeBox = false;
            MaximizeBox = false;
            Width = 380;
            Height = 176;
            Text = "Bitte warten";

            _bar = new ProgressBar { Dock = DockStyle.Bottom, Height = 22, Style = ProgressBarStyle.Marquee, MarqueeAnimationSpeed = 30 };
            _label = new Label
            {
                Dock = DockStyle.Top,
                Height = 84,
                TextAlign = ContentAlignment.MiddleCenter,
                Text = _message
            };
            Controls.Add(_bar);
            Controls.Add(_label);
        }

        /// <summary>Zeigt Prozent (Balken + Text, z. B. "Download: 75 %"); Percent &lt; 0 = unbestimmt (laufender Balken).</summary>
        private void ShowProgress(int percent, string? step)
        {
            if (percent < 0)
            {
                _bar.Style = ProgressBarStyle.Marquee;
                _label.Text = string.IsNullOrEmpty(step) ? _message : $"{_message}\n{step}";
                return;
            }
            _bar.Style = ProgressBarStyle.Continuous;
            _bar.Value = Math.Clamp(percent, 0, 100);
            _label.Text = $"{_message}\n{(string.IsNullOrEmpty(step) ? "" : step + ": ")}{percent} %";
        }

        /// <summary>
        /// Wie <see cref="RunAsync(IWin32Window, string, Func{Task})"/>, die Operation kann aber
        /// Fortschritt melden (Prozent, Schritt). Meldungen aus Hintergrund-Threads sind erlaubt.
        /// </summary>
        public static async Task RunAsync(IWin32Window owner, string message, Func<IProgress<(int Percent, string? Step)>, Task> operation)
        {
            Task? task = null;
            using (var dlg = new WaitDialog(message))
            {
                dlg.Shown += async (s, e) =>
                {
                    // Progress<T> im UI-Thread erzeugt -> Rückmeldungen landen im UI-Thread
                    var progress = new Progress<(int Percent, string? Step)>(p => { if (!dlg.IsDisposed) dlg.ShowProgress(p.Percent, p.Step); });
                    task = operation(progress);
                    try { await task; } catch { /* wird unten an den Aufrufer weitergegeben */ }
                    dlg.Close();
                };
                dlg.ShowDialog(owner);
            }

            if (task != null)
                await task;
        }

        /// <summary>
        /// Zeigt den Dialog, führt die Operation aus und schließt ihn danach.
        /// Exceptions der Operation werden an den Aufrufer weitergereicht.
        /// </summary>
        public static async Task RunAsync(IWin32Window owner, string message, Func<Task> operation)
        {
            Task? task = null;
            using (var dlg = new WaitDialog(message))
            {
                dlg.Shown += async (s, e) =>
                {
                    task = operation();
                    try { await task; } catch { /* wird unten an den Aufrufer weitergegeben */ }
                    dlg.Close();
                };
                dlg.ShowDialog(owner);
            }

            if (task != null)
                await task;
        }
    }
}
