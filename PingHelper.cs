// PingHelper.cs
using System;
using System.Net;
using System.Net.NetworkInformation;
using System.Threading;
using System.Threading.Tasks;

namespace DhcpWmiViewer
{
    /// <summary>
    /// Ergebnis eines Ping-Vorgangs.
    /// </summary>
    public class PingResult
    {
        public bool Success { get; set; }
        public long RoundtripTime { get; set; }
        public IPStatus Status { get; set; }
        public string ErrorMessage { get; set; } = "";
    }

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

        /// <summary>
        /// Ping mit detailliertem Ergebnis.
        /// </summary>
        public static async Task<PingResult> PingAsync(string hostOrIp, int timeoutMs = 5000)
        {
            var result = new PingResult();
            
            if (string.IsNullOrWhiteSpace(hostOrIp))
            {
                result.ErrorMessage = "Host or IP address is empty";
                return result;
            }

            using var ping = new Ping();
            try
            {
                var reply = await ping.SendPingAsync(hostOrIp, timeoutMs);
                
                if (reply != null)
                {
                    result.Success = reply.Status == IPStatus.Success;
                    result.RoundtripTime = reply.RoundtripTime;
                    result.Status = reply.Status;
                    
                    if (!result.Success)
                    {
                        result.ErrorMessage = reply.Status.ToString();
                    }
                }
                else
                {
                    result.ErrorMessage = "No reply received";
                }
            }
            catch (Exception ex)
            {
                result.ErrorMessage = ex.Message;
            }

            return result;
        }
    }
}
