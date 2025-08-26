// PingHelper.cs
using System;
using System.Net;
using System.Net.NetworkInformation;
using System.Threading;
using System.Threading.Tasks;

namespace DhcpWmiViewer
{
    public static class PingHelper
    {
        /// <summary>
        /// Ping einmal asynchron. Liefert true bei erfolgreichem Reply.
        /// Unterstützt CancellationToken (sowie Timeout).
        /// </summary>
        public static async Task<bool> PingOnceAsync(string ipString, int timeoutMs, CancellationToken ct)
        {
            if (string.IsNullOrWhiteSpace(ipString)) return false;
            if (!IPAddress.TryParse(ipString, out _)) return false;
            if (timeoutMs <= 0) timeoutMs = 700;

            using var ping = new Ping();
            try
            {
                var sendTask = ping.SendPingAsync(ipString, timeoutMs);

                // Warte entweder auf das Ping-Task oder auf eine Delay-Task, die mit dem CancellationToken verbunden ist.
                var delayTask = Task.Delay(timeoutMs + 500, ct); // kleiner Puffer
                var completed = await Task.WhenAny(sendTask, delayTask).ConfigureAwait(false);

                // Falls nicht das sendTask zuerst fertig wurde -> abgebrochen/timeout
                if (completed != sendTask) return false;

                // sendTask ist abgeschlossen — Ergebnis auswerten
                var reply = await sendTask.ConfigureAwait(false);
                return reply != null && reply.Status == IPStatus.Success;
            }
            catch (OperationCanceledException)
            {
                // abgebrochen durch CancellationToken
                return false;
            }
            catch
            {
                // bei allen anderen Fehlern: false zurückgeben (robust für UI)
                return false;
            }
        }
    }
}
