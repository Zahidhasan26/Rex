using System.Net;
using System.Net.Sockets;
using System.Text;
using UnityEngine;

namespace BowGame
{
    // Sends vest hits as text like "L,200" over UDP port 5006. Plugs itself into Haptics.Hit.
    public class VestSender : MonoBehaviour
    {
        public int port = 5006;
        [Tooltip("Leave empty to broadcast to everything on the network.")]
        public string vestIp = "";
        public bool logHits = true;
        [Tooltip("Sends a silent \"P,0\" this often so the ESP32 learns the laptop's address right away " +
                 "and starts sending the muscle signal straight to it (some hotspots drop broadcasts).")]
        public float pingInterval = 1f;
        float nextPing;

        UdpClient udp;
        IPEndPoint target;

        void OnEnable()
        {
            try
            {
                udp = new UdpClient();
                udp.EnableBroadcast = true;
                var ip = string.IsNullOrEmpty(vestIp) ? IPAddress.Broadcast : IPAddress.Parse(vestIp);
                target = new IPEndPoint(ip, port);
            }
            catch (System.Exception e) { Debug.LogWarning("[VestSender] " + e.Message); }
            Haptics.Sender = Send;
        }

        void OnDisable()
        {
            if (Haptics.Sender == Send) Haptics.Sender = null;
            if (udp != null) { udp.Close(); udp = null; }
        }

        void Update()
        {
            if (udp == null || pingInterval <= 0f || Time.unscaledTime < nextPing) return;
            nextPing = Time.unscaledTime + pingInterval;
            SendText("P,0");
        }

        public void SendText(string msg)
        {
            var bytes = Encoding.ASCII.GetBytes(msg);
            // Once the ESP32 has been heard from, talk to it directly; until then, broadcast (or the fixed vestIp).
            var to = EmgReceiver.Esp32Address != null && string.IsNullOrEmpty(vestIp)
                ? new IPEndPoint(EmgReceiver.Esp32Address, port) : target;
            if (to == null) return;
            // Prefer the armband port, so the ESP32's muscle data comes back as a reply the firewall allows.
            if (EmgReceiver.Instance != null && EmgReceiver.Instance.SendFromListenPort(bytes, to)) return;
            if (udp == null) return;
            try { udp.Send(bytes, bytes.Length, to); }
            catch (System.Exception e) { Debug.LogWarning("[VestSender] send failed: " + e.Message); }
        }

        void Send(HitSide side, float strength01)
        {
            string letter = side == HitSide.Left ? "L" : side == HitSide.Right ? "R" : "F";
            string msg = letter + "," + Mathf.RoundToInt(Mathf.Clamp01(strength01) * 255f);
            if (logHits) Debug.Log("[Vest] " + msg);
            SendText(msg);
        }
    }
}
