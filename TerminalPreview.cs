// TerminalPreview.cs
using System;
using System.Threading.Tasks;
using System.Windows.Forms;
using System.Drawing;

namespace DhcpWmiViewer
{
    internal static class TerminalPreview
    {
        /// <summary>
        /// Asynchrone Anzeige des PowerShell-Preview-Dialogs.
        /// Liefert true bei DialogResult.OK (Run), false sonst.
        /// Nicht-blockierend: der aufrufende Thread wird nicht blockiert.
        /// </summary>
        public static Task<bool> ShowTerminalAsync(string command)
        {
            return ShowTerminalAsync(null, command);
        }

        /// <summary>
        /// Asynchrone Anzeige des PowerShell-Preview-Dialogs mit optionalem Owner.
        /// Wenn owner != null und es läuft bereits eine MessageLoop, wird das Dialog
        /// auf dem UI-Thread des Owners erzeugt und modeless gezeigt (Owner wird temporär deaktiviert,
        /// um modales Verhalten zu simulieren). Falls keine MessageLoop vorhanden ist, wird ein STA-Thread
        /// gestartet und das Formular darin modal via Application.Run ausgeführt.
        /// </summary>
        public static Task<bool> ShowTerminalAsync(Form? owner, string command)
        {
            var tcs = new TaskCompletionSource<bool>();

            // If preview suppressed by DhcpManager.ShowCommandPreview, return true quickly
            if (!DhcpManager.ShowCommandPreview)
            {
                tcs.SetResult(true);
                return tcs.Task;
            }

            // If we have a WinForms message loop on this process:
            if (Application.MessageLoop)
            {
                // Determine a real owner (fallback to provided owner or first open form)
                Form? actualOwner = owner;
                if (actualOwner == null && Application.OpenForms.Count > 0)
                    actualOwner = Application.OpenForms[0];

                // If we have an owner, create the dialog on the owner's UI thread.
                if (actualOwner != null)
                {
                    try
                    {
                        // Create & show form on owner's thread using Invoke to be safe
                        actualOwner.BeginInvoke(new Action(() =>
                        {
                            var frm = CreateForm(command);

                            // When closed, complete the TCS and re-enable owner
                            frm.FormClosed += (s, e) =>
                            {
                                try
                                {
                                    try { actualOwner.Enabled = true; } catch { }
                                    tcs.TrySetResult(frm.DialogResult == DialogResult.OK);
                                }
                                catch (Exception ex)
                                {
                                    tcs.TrySetException(ex);
                                }
                                finally
                                {
                                    try { frm.Dispose(); } catch { }
                                }
                            };

                            // Disable owner to simulate modal behavior, then show modeless
                            try { actualOwner.Enabled = false; } catch { }
                            frm.Show(actualOwner);
                        }));

                        return tcs.Task;
                    }
                    catch (Exception ex)
                    {
                        tcs.SetException(ex);
                        return tcs.Task;
                    }
                }
                else
                {
                    // MessageLoop is present but no owner available -> show modeless on main thread
                    try
                    {
                        var frm = CreateForm(command);
                        frm.FormClosed += (s, e) =>
                        {
                            try { tcs.TrySetResult(frm.DialogResult == DialogResult.OK); } catch (Exception ex) { tcs.TrySetException(ex); }
                            finally { try { frm.Dispose(); } catch { } }
                        };
                        frm.Show();
                        return tcs.Task;
                    }
                    catch (Exception ex)
                    {
                        tcs.SetException(ex);
                        return tcs.Task;
                    }
                }
            }
            else
            {
                // No WinForms message loop (likely running in non-UI thread / console). Start STA thread.
                var thread = new System.Threading.Thread(() =>
                {
                    try
                    {
                        // Create form on STA thread and run message loop until closed.
                        using (var frm = CreateForm(command))
                        {
                            frm.FormClosed += (s, e) =>
                            {
                                try { tcs.TrySetResult(frm.DialogResult == DialogResult.OK); } catch (Exception ex) { tcs.TrySetException(ex); }
                                // don't call Application.ExitThread() here; we'll exit after ShowDialog returns.
                            };

                            // Run modal dialog so that ShowDialog blocks this STA thread only
                            frm.ShowDialog();
                        }
                    }
                    catch (Exception ex)
                    {
                        try { tcs.TrySetException(ex); } catch { }
                    }
                    finally
                    {
                        try { Application.ExitThread(); } catch { }
                    }
                });

                thread.SetApartmentState(System.Threading.ApartmentState.STA);
                thread.IsBackground = true;
                thread.Start();

                return tcs.Task;
            }
        }

        /// <summary>
        /// Compatibility synchronous wrapper.
        /// NOTE: this blocks the calling thread until the preview is closed.
        /// Prefer using ShowTerminalAsync(...) instead to avoid UI freezes.
        /// </summary>
        public static bool ShowTerminal(string command)
        {
            try
            {
                // blockingly wait for async version — exists for compatibility only.
                return ShowTerminalAsync(command).GetAwaiter().GetResult();
            }
            catch
            {
                return false;
            }
        }

        private static Form CreateForm(string command)
        {
            var frm = new Form()
            {
                Text = "PowerShell Command Preview",
                Width = 900,
                Height = 420,
                StartPosition = FormStartPosition.CenterParent,
                Font = new Font("Segoe UI", 9F)
            };

            var txt = new TextBox()
            {
                Multiline = true,
                ReadOnly = true,
                Dock = DockStyle.Fill,
                ScrollBars = ScrollBars.Both,
                Font = new Font("Consolas", 10),
                Text = command
            };

            var btnRun = new Button() { Text = "Run", DialogResult = DialogResult.OK, AutoSize = true, Padding = new Padding(6) };
            var btnCancel = new Button() { Text = "Cancel", DialogResult = DialogResult.Cancel, AutoSize = true, Padding = new Padding(6) };
            var btnCopy = new Button() { Text = "Copy", AutoSize = true, Padding = new Padding(6) };

            btnCopy.Click += (s, e) =>
            {
                try { Clipboard.SetText(command); } catch { }
            };

            var panel = new FlowLayoutPanel()
            {
                Dock = DockStyle.Bottom,
                Height = 48,
                FlowDirection = FlowDirection.RightToLeft,
                Padding = new Padding(8)
            };

            panel.Controls.Add(btnRun);
            panel.Controls.Add(btnCancel);
            panel.Controls.Add(btnCopy);

            frm.Controls.Add(txt);
            frm.Controls.Add(panel);

            frm.AcceptButton = btnRun;
            frm.CancelButton = btnCancel;

            return frm;
        }
    }
}
