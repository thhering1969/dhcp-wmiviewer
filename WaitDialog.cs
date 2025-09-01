// WaitDialog.cs
using System;
using System.Drawing;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace DhcpWmiViewer
{
	internal sealed class WaitDialog : Form
	{
		private readonly Label lbl;
		private readonly ProgressBar bar;

		private WaitDialog(string message)
		{
			this.FormBorderStyle = FormBorderStyle.FixedDialog;
			this.ControlBox = false;
			this.StartPosition = FormStartPosition.CenterParent;
			this.MinimizeBox = false;
			this.MaximizeBox = false;
			this.Width = 360;
			this.Height = 120;
			this.Text = "Bitte warten";

			lbl = new Label { Dock = DockStyle.Top, Height = 40, TextAlign = ContentAlignment.MiddleCenter };
			bar = new ProgressBar { Dock = DockStyle.Bottom, Height = 22, Style = ProgressBarStyle.Marquee, MarqueeAnimationSpeed = 30 };

			this.Controls.Add(bar);
			this.Controls.Add(lbl);
			SetMessage(message);
		}

		private void SetMessage(string message)
		{
			try { lbl.Text = string.IsNullOrWhiteSpace(message) ? "Bitte warten…" : message; } catch { }
		}

		public static async Task RunAsync(IWin32Window owner, string message, Func<Task> operation)
		{
			if (operation == null) return;
			using (var dlg = new WaitDialog(message))
			{
				// Zeige nicht-blockierend, Operation läuft im Hintergrund, Dialog bleibt modal
				dlg.Shown += async (s, e) =>
				{
					try { await operation().ConfigureAwait(false); }
					finally { try { dlg.Close(); } catch { } }
				};
				dlg.ShowDialog(owner);
			}
		}
	}
}


