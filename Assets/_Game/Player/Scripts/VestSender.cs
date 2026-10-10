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

        void Send(HitSide side, float strength01)
        {
            string letter = side == HitSide.Left ? "L" : side == HitSide.Right ? "R" : "F";
            string msg = letter + "," + Mathf.RoundToInt(Mathf.Clamp01(strength01) * 255f);
            if (logHits) Debug.Log("[Vest] " + msg);
            if (udp == null) return;
            try { var bytes = Encoding.ASCII.GetBytes(msg); udp.Send(bytes, bytes.Length, target); }
            catch (System.Exception e) { Debug.LogWarning("[VestSender] send failed: " + e.Message); }
        }
    }
}
