// MainForm.Cleanup.cs
using System;
using System.ComponentModel;
using System.Windows.Forms;

namespace DhcpWmiViewer
{
    public partial class MainForm
    {
        /// <summary>
        /// Cleanup bei Form-Close: Stoppe alle Timer und räume Ressourcen auf
        /// </summary>
        private void MainForm_FormClosing(object sender, FormClosingEventArgs e)
        {
            try
            {
                DebugLogger.Log("MainForm cleanup starting...");

                // Stoppe AD Online-Status Timer
                StopADOnlineStatusRefresh();
                
                // Stoppe Refresh-Timer aus CustomDrawing
                if (_refreshTimer != null)
                {
                    _refreshTimer.Stop();
                    _refreshTimer.Dispose();
                    _refreshTimer = null;
                    DebugLogger.Log("CustomDrawing refresh timer stopped");
                }
                
                // Leere Online-Status Cache
                ComputerOnlineChecker.ClearCache();
                
                DebugLogger.Log("MainForm cleanup completed - all timers stopped");
            }
            catch (Exception ex)
            {
                DebugLogger.LogFormat("Error during MainForm cleanup: {0}", ex.Message);
            }
        }
    }
}