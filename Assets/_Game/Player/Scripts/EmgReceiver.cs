using System.Globalization;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using UnityEngine;
using UnityEngine.InputSystem;

namespace BowGame
{
    // Feeds EmgInput.Value. Listens for the armband on UDP port 5005 (text number 0..1).
    // When no armband data is arriving, a controller trigger (or the Space key) stands in for the muscle.
    public class EmgReceiver : MonoBehaviour
    {
        public int port = 5005;
        public float hardwareTimeout = 1f;     // seconds without packets before falling back to the trigger
        public float smoothing = 18f;          // higher = snappier

        UdpClient udp;
        Thread thread;
        volatile bool running;
        volatile float latest;
        long lastPacketTicks;

        // ---- diagnostics (read by the EMG test panel)
        public static int PacketCount;          // every packet that arrived, readable or not
        public static int BadPackets;           // packets whose text wasn't a number
        public static string LastText = "";     // the last packet as text
        public static string LastSender = "";   // ip:port it came from
        public static bool PortOpen;            // false if the UDP port couldn't be opened
        static long lastAnyPacketTicks;         // any packet at all, readable or not
        /// Seconds since anything arrived from the ESP32 (infinity if never).
        public static float SecondsSinceEsp32 =>
            Interlocked.Read(ref lastAnyPacketTicks) == 0 ? float.PositiveInfinity
            : (float)((System.DateTime.UtcNow.Ticks - Interlocked.Read(ref lastAnyPacketTicks)) / 1e7);
        InputAction fallback;
        float smoothed;

        // ---- calibration: the armband sends its raw level (0..1); relax and flex set the range the game uses
        public static EmgReceiver Instance;

        /// The ESP32's address, learned from its packets (null until it has sent something).
        public static IPAddress Esp32Address;

        /// Sends from the SAME port the armband data arrives on (5005). The ESP32 answers to the port
        /// it heard from, so its data comes back as a reply on this conversation and Windows Firewall
        /// lets it in without an admin rule.
        public bool SendFromListenPort(byte[] data, IPEndPoint to)
        {
            var u = udp;
            if (u == null) return false;
            try { u.Send(data, data.Length, to); return true; }
            catch (System.Exception e) { Debug.LogWarning("[EmgReceiver] send failed: " + e.Message); return false; }
        }
        [Tooltip("Raw level when the arm is relaxed (set by calibration).")]
        public float relaxLevel = 0.05f;
        [Tooltip("Raw level when flexing hard (set by calibration).")]
        public float flexLevel = 0.6f;
        public float RawLevel => latest;
        enum Cal { None, Relax, Flex }
        Cal cal;
        float calSum, calMax; int calCount;

        public void StartRelaxCalibration() { cal = Cal.Relax; calSum = 0f; calCount = 0; }
        public void StartFlexCalibration()  { cal = Cal.Flex;  calMax = 0f; }
        public void EndCalibration()
        {
            if (cal == Cal.Flex && calMax > relaxLevel + 0.05f) flexLevel = calMax * 0.85f;   // full draw without straining
            cal = Cal.None;
            Debug.Log("[EmgReceiver] calibrated: relax " + relaxLevel.ToString("0.000") + ", flex " + flexLevel.ToString("0.000"));
        }

        void OnEnable()
        {
            fallback = new InputAction("FlexFallback", InputActionType.Value);
            fallback.AddBinding("<XRController>{LeftHand}/trigger");
            fallback.AddBinding("<XRController>{RightHand}/trigger");
            fallback.AddBinding("<Keyboard>/space");
            fallback.Enable();
            Instance = this;

            try
            {
                udp = new UdpClient(port);
                udp.EnableBroadcast = true;
                PortOpen = true;
                running = true;
                thread = new Thread(Listen) { IsBackground = true };
                thread.Start();
            }
            catch (System.Exception e)
            {
                PortOpen = false;
                Debug.LogWarning("[EmgReceiver] Could not open UDP port " + port + ": " + e.Message);
            }
        }

        void OnDisable()
        {
            running = false;
            if (udp != null) { udp.Close(); udp = null; }
            if (fallback != null) { fallback.Disable(); fallback.Dispose(); fallback = null; }
            EmgInput.Value = 0f;
            EmgInput.HardwareConnected = false;
            if (Instance == this) Instance = null;
        }

        void Listen()
        {
            var ep = new IPEndPoint(IPAddress.Any, 0);
            while (running)
            {
                try
                {
                    var data = udp.Receive(ref ep);
                    var text = Encoding.ASCII.GetString(data).Trim();
                    PacketCount++;
                    Interlocked.Exchange(ref lastAnyPacketTicks, System.DateTime.UtcNow.Ticks);
                    LastText = text;
                    LastSender = ep.ToString();
                    Esp32Address = ep.Address;
                    if (float.TryParse(text, NumberStyles.Float, CultureInfo.InvariantCulture, out var v))
                    {
                        latest = Mathf.Clamp01(v);
                        Interlocked.Exchange(ref lastPacketTicks, System.DateTime.UtcNow.Ticks);
                    }
                    else BadPackets++;
                }
                catch { /* socket closed or bad packet */ }
            }
        }

        void Update()
        {
            long last = Interlocked.Read(ref lastPacketTicks);
            bool connected = last != 0 && (System.DateTime.UtcNow.Ticks - last) < (long)(hardwareTimeout * 10000000L);
            if (connected != EmgInput.HardwareConnected)
                Debug.Log(connected ? "[EmgReceiver] armband connected (" + LastSender + ")"
                                    : "[EmgReceiver] armband lost, no data for " + hardwareTimeout + " s, using the trigger");
            EmgInput.HardwareConnected = connected;

            float raw = latest;
            if (connected && cal == Cal.Relax) { calSum += raw; calCount++; relaxLevel = calSum / calCount; }
            if (connected && cal == Cal.Flex) calMax = Mathf.Max(calMax, raw);
            float level = Mathf.InverseLerp(relaxLevel + 0.02f, Mathf.Max(relaxLevel + 0.05f, flexLevel), raw);
            float target = connected ? level : (fallback != null ? fallback.ReadValue<float>() : 0f);
            smoothed = Mathf.Lerp(smoothed, target, 1f - Mathf.Exp(-smoothing * Time.deltaTime));
            EmgInput.Value = Mathf.Clamp01(smoothed);
        }
    }
}
