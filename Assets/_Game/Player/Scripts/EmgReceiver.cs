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
        InputAction fallback;
        float smoothed;

        void OnEnable()
        {
            fallback = new InputAction("FlexFallback", InputActionType.Value);
            fallback.AddBinding("<XRController>{LeftHand}/trigger");
            fallback.AddBinding("<XRController>{RightHand}/trigger");
            fallback.AddBinding("<Keyboard>/space");
            fallback.Enable();

            try
            {
                udp = new UdpClient(port);
                running = true;
                thread = new Thread(Listen) { IsBackground = true };
                thread.Start();
            }
            catch (System.Exception e)
            {
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
                    if (float.TryParse(text, NumberStyles.Float, CultureInfo.InvariantCulture, out var v))
                    {
                        latest = Mathf.Clamp01(v);
                        Interlocked.Exchange(ref lastPacketTicks, System.DateTime.UtcNow.Ticks);
                    }
                }
                catch { /* socket closed or bad packet */ }
            }
        }

        void Update()
        {
            long last = Interlocked.Read(ref lastPacketTicks);
            bool connected = last != 0 && (System.DateTime.UtcNow.Ticks - last) < (long)(hardwareTimeout * 10000000L);
            EmgInput.HardwareConnected = connected;

            float target = connected ? latest : (fallback != null ? fallback.ReadValue<float>() : 0f);
            smoothed = Mathf.Lerp(smoothed, target, 1f - Mathf.Exp(-smoothing * Time.deltaTime));
            EmgInput.Value = Mathf.Clamp01(smoothed);
        }
    }
}
