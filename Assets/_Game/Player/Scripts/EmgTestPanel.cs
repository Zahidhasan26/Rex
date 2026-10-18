using System.Net;
using System.Net.Sockets;
using UnityEngine;
using UnityEngine.InputSystem;

namespace BowGame
{
    // EMG bench test (no bow, no game). Shows on the Game view exactly what arrives from the armband,
    // so you can tell whether a problem is the network, the firewall, or the sensor itself.
    // Keys: 1 = calibrate relax (keep arm loose), 2 = calibrate flex (squeeze fist), 3 = reset calibration,
    //       L / R / F = buzz the vest (left / right / all) to check the link the other way.
    public class EmgTestPanel : MonoBehaviour
    {
        EmgReceiver receiver;
        VestSender vest;
        string laptopIps = "";
        int lastCount; float rate, rateTimer;
        float peakRaw, calEndsAt; string calLabel = "";
        readonly float[] history = new float[200]; int histIndex;
        GUIStyle big, mid, small;
        Texture2D white;

        void Awake()
        {
            receiver = FindAnyObjectByType<EmgReceiver>();
            if (receiver == null) receiver = gameObject.AddComponent<EmgReceiver>();
            vest = FindAnyObjectByType<VestSender>();
            if (vest == null) vest = gameObject.AddComponent<VestSender>();
            try
            {
                foreach (var a in Dns.GetHostAddresses(Dns.GetHostName()))
                    if (a.AddressFamily == AddressFamily.InterNetwork) laptopIps += (laptopIps.Length > 0 ? ", " : "") + a;
            }
            catch { laptopIps = "(unknown)"; }
            white = Texture2D.whiteTexture;
        }

        void Update()
        {
            rateTimer += Time.unscaledDeltaTime;
            if (rateTimer >= 1f) { rate = (EmgReceiver.PacketCount - lastCount) / rateTimer; lastCount = EmgReceiver.PacketCount; rateTimer = 0f; }
            float raw = receiver != null ? receiver.RawLevel : 0f;
            history[histIndex] = raw; histIndex = (histIndex + 1) % history.Length;
            peakRaw = Mathf.Max(peakRaw * 0.995f, raw);

            var k = Keyboard.current;
            if (k == null || receiver == null) return;
            if (k.digit1Key.wasPressedThisFrame) { receiver.StartRelaxCalibration(); calLabel = "RELAX: keep the arm loose"; calEndsAt = Time.time + 3f; }
            if (k.digit2Key.wasPressedThisFrame) { receiver.StartFlexCalibration(); calLabel = "FLEX: squeeze your fist hard"; calEndsAt = Time.time + 3f; }
            if (k.digit3Key.wasPressedThisFrame) { receiver.relaxLevel = 0.05f; receiver.flexLevel = 0.6f; }
            if (calEndsAt > 0f && Time.time >= calEndsAt) { receiver.EndCalibration(); calEndsAt = 0f; calLabel = ""; }
            if (vest != null)
            {
                if (k.lKey.wasPressedThisFrame) vest.SendText("L,255");
                if (k.rKey.wasPressedThisFrame) vest.SendText("R,255");
                if (k.fKey.wasPressedThisFrame) vest.SendText("F,255");
            }
        }

        string Diagnosis()
        {
            if (!EmgReceiver.PortOpen) return "UDP port 5005 could not be opened. Close any other Unity window / game that is running, then press Play again.";
            if (EmgReceiver.PacketCount == 0)
                return "NOTHING is arriving from the ESP32.\n" +
                       "Press L: if the vest buzzes, Wi-Fi works and Windows Firewall is blocking incoming data " +
                       "(allow Unity Editor on Public + Private). No buzz: the ESP32 is not on the laptop's network " +
                       "(its IP must start like the laptop IP above).";
            if (rate < 1f) return "Data stopped arriving. ESP32 restarted or left the Wi-Fi?";
            if (EmgReceiver.BadPackets > 0 && !EmgInput.HardwareConnected) return "Packets arrive but aren't numbers. Last text: \"" + EmgReceiver.LastText + "\"";
            if (peakRaw < 0.02f) return "Data arrives, but the sensor reads ~0. Check ENV -> pin 32, VIN -> 3V, GND, and the MyoWare power LED.";
            return "Link OK. Squeeze your fist: the RAW bar should jump. Then press 1 (relax) and 2 (flex) to calibrate.";
        }

        void OnGUI()
        {
            if (big == null)
            {
                big = new GUIStyle(GUI.skin.label) { fontSize = 30, fontStyle = FontStyle.Bold };
                mid = new GUIStyle(GUI.skin.label) { fontSize = 20, wordWrap = true };
                small = new GUIStyle(GUI.skin.label) { fontSize = 16 };
            }
            float w = Mathf.Min(Screen.width - 40f, 900f), x = 20f, y = 20f;
            GUI.color = new Color(0f, 0f, 0f, 0.8f);
            GUI.DrawTexture(new Rect(10, 10, w + 20, 640), white);
            GUI.color = Color.white;

            // ESP32 link: is anything arriving from the board at all?
            float since = EmgReceiver.SecondsSinceEsp32;
            bool espOn = since < 2f;
            string esp = espOn ? "ESP32: CONNECTED  (" + EmgReceiver.LastSender + ")"
                       : float.IsInfinity(since) ? "ESP32: NOT SEEN YET"
                       : "ESP32: LOST  (silent for " + since.ToString("0") + " s)";
            GUI.color = espOn ? Color.green : Color.red;
            GUI.Label(new Rect(x, y, w, 40), esp, big); y += 40;

            // EMG sensor: connected board + a signal that actually moves
            bool on = EmgInput.HardwareConnected;
            bool moving = peakRaw > 0.02f;
            GUI.color = !on ? Color.red : moving ? Color.green : Color.yellow;
            GUI.Label(new Rect(x, y, w, 40), !on ? "EMG: NO DATA" : moving ? "EMG: SIGNAL OK" : "EMG: DATA BUT FLAT (check sensor)", big); y += 44;
            GUI.color = Color.white;
            GUI.Label(new Rect(x, y, w, 24), "Laptop IP: " + laptopIps + "     listening on UDP 5005: " + (EmgReceiver.PortOpen ? "yes" : "NO"), small); y += 22;
            GUI.Label(new Rect(x, y, w, 24), "Packets: " + EmgReceiver.PacketCount + "  (" + rate.ToString("0") + "/s, ESP32 sends 50/s)   bad: " + EmgReceiver.BadPackets, small); y += 22;
            GUI.Label(new Rect(x, y, w, 24), "From: " + (EmgReceiver.LastSender == "" ? "-" : EmgReceiver.LastSender) + "   last text: \"" + EmgReceiver.LastText + "\"", small); y += 30;

            float raw = receiver != null ? receiver.RawLevel : 0f;
            Bar(ref y, x, w, "RAW from sensor", raw, new Color(0.3f, 0.7f, 1f));
            Bar(ref y, x, w, "Game value (calibrated, pulls the bow)", EmgInput.Value, new Color(1f, 0.6f, 0.2f));
            if (receiver != null)
                GUI.Label(new Rect(x, y, w, 24), "relax " + receiver.relaxLevel.ToString("0.000") + "   flex " + receiver.flexLevel.ToString("0.000") + "   peak " + peakRaw.ToString("0.000"), small);
            y += 28;

            // last 4 seconds of the raw signal
            var graph = new Rect(x, y, w, 90);
            GUI.color = new Color(1f, 1f, 1f, 0.08f); GUI.DrawTexture(graph, white); GUI.color = new Color(0.3f, 0.7f, 1f);
            float colW = w / history.Length;
            for (int i = 0; i < history.Length; i++)
            {
                float v = Mathf.Clamp01(history[(histIndex + i) % history.Length]);
                GUI.DrawTexture(new Rect(x + i * colW, graph.yMax - v * graph.height, Mathf.Max(1f, colW), v * graph.height), white);
            }
            GUI.color = Color.white; y += 98;

            if (calLabel != "") { GUI.color = Color.yellow; GUI.Label(new Rect(x, y, w, 30), calLabel, mid); GUI.color = Color.white; }
            y += 30;
            GUI.Label(new Rect(x, y, w, 90), Diagnosis(), mid); y += 92;
            GUI.Label(new Rect(x, y, w, 24), "Keys: 1 relax  2 flex  3 reset   |   L / R / F buzz the vest", small);
        }

        void Bar(ref float y, float x, float w, string label, float v, Color c)
        {
            GUI.Label(new Rect(x, y, w, 22), label + ": " + v.ToString("0.000"), small); y += 22;
            var r = new Rect(x, y, w, 22);
            GUI.color = new Color(1f, 1f, 1f, 0.12f); GUI.DrawTexture(r, white);
            GUI.color = c; GUI.DrawTexture(new Rect(r.x, r.y, r.width * Mathf.Clamp01(v), r.height), white);
            GUI.color = Color.white; y += 28;
        }
    }
}
