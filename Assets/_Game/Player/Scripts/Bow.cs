using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.XR;

namespace BowGame
{
    // The bow. Sits on the left controller; its forward (blue) axis is where arrows go.
    // Hold the RIGHT trigger to grab the string, move the right hand back to draw, let go of the trigger to fire.
    // When the EMG armband is connected, flexing also grabs the string and relaxing releases it.
    public class Bow : MonoBehaviour
    {
        public static float CurrentDraw;       // 0..1, read by the HUD

        [Header("Parts (set up by the Player prefab)")]
        public Transform stringTop;
        public Transform stringBottom;
        public Transform nock;                 // rest position of the middle of the string
        public Transform nockedArrow;          // the arrow you see on the string
        public LineRenderer stringLine;
        public LineRenderer aimLine;
        public Transform aimDot;
        public Arrow arrowPrefab;
        public Transform ownerRoot;            // the player rig, so arrows ignore the player's own colliders
        public Transform trackingSpace;        // the rig's "Camera Offset"; found automatically if left empty

        [Header("Pull")]
        public float fullPullDistance = 0.45f; // how far the right hand must move back for a full draw (metres)
        public float grabThreshold = 0.5f;     // trigger (or flex) above this grabs the string
        public float releaseThreshold = 0.3f;  // trigger (or flex) below this lets go
        public float minFireDraw = 0.12f;      // pulls weaker than this are cancelled, not fired
        public bool emgCanGrab = true;         // with the armband connected, flexing grabs the string too

        [Header("Feel")]
        public float maxDrawDistance = 0.45f;  // how far the string visibly pulls back at full draw (metres)
        public float minSpeed = 14f;           // arrow speed at the weakest shot (m/s)
        public float maxSpeed = 55f;           // arrow speed at full draw
        public float cooldown = 0.35f;

        [Header("Aim helper")]
        public bool showAim = true;
        public int aimPoints = 40;
        public float aimStep = 0.04f;

        [Header("Sounds (optional)")]
        public AudioSource audioSource;
        public AudioClip drawClip;
        public AudioClip releaseClip;
        [Tooltip("Arrow whoosh played on release, louder for a stronger shot.")]
        public AudioClip flightClip;
        [Range(0f, 1f)] public float flightVolume = 0.7f;

        InputAction rightTrigger;
        InputAction rightPosition;
        bool holding;
        float draw;
        float separationAtGrab;
        float lastTick;
        float nextAllowed;
        Vector3 nockRest;
        Vector3[] aimBuffer;

        void OnEnable()
        {
            rightTrigger = new InputAction("BowGrab", InputActionType.Value, "<XRController>{RightHand}/trigger");
            rightPosition = new InputAction("BowHand", InputActionType.Value, "<XRController>{RightHand}/devicePosition");
            rightTrigger.Enable();
            rightPosition.Enable();
        }

        void OnDisable()
        {
            if (rightTrigger != null) { rightTrigger.Disable(); rightTrigger.Dispose(); rightTrigger = null; }
            if (rightPosition != null) { rightPosition.Disable(); rightPosition.Dispose(); rightPosition = null; }
            holding = false; draw = 0f; CurrentDraw = 0f;
        }

        void Start()
        {
            if (nock != null) nockRest = nock.localPosition;
            aimBuffer = new Vector3[Mathf.Max(2, aimPoints)];
            if (stringLine != null) stringLine.positionCount = 3;
            if (trackingSpace == null && ownerRoot != null)
                foreach (var t in ownerRoot.GetComponentsInChildren<Transform>(true))
                    if (t.name == "Camera Offset") { trackingSpace = t; break; }
            if (trackingSpace == null && transform.parent != null) trackingSpace = transform.parent.parent;
            SetAimVisible(false);
        }

        // How hard the player is "holding the string": the right trigger, or the muscle flex when the armband is live.
        float GrabValue()
        {
            float v = rightTrigger != null ? rightTrigger.ReadValue<float>() : 0f;
            if (emgCanGrab && EmgInput.HardwareConnected) v = Mathf.Max(v, EmgInput.Value);
            return v;
        }

        // Distance from the right hand to the bow, measured along the direction the bow points.
        float HandSeparation()
        {
            Vector3 local = rightPosition != null ? rightPosition.ReadValue<Vector3>() : Vector3.zero;
            Vector3 hand = trackingSpace != null ? trackingSpace.TransformPoint(local) : local;
            return Vector3.Dot(transform.position - hand, transform.forward);
        }

        void Update()
        {
            float grab = GrabValue();
            bool canShoot = GameRefs.State != GameState.Dead && Time.time >= nextAllowed;

            if (!holding)
            {
                if (canShoot && grab > grabThreshold)
                {
                    holding = true;
                    separationAtGrab = HandSeparation();
                    draw = 0f; lastTick = 0f;
                    if (audioSource != null && drawClip != null) audioSource.PlayOneShot(drawClip);
                }
            }
            else
            {
                float pulled = HandSeparation() - separationAtGrab;
                draw = Mathf.Clamp01(pulled / Mathf.Max(0.05f, fullPullDistance));

                // A light tick in the right hand every 10% of draw, so the pull can be felt.
                if (Mathf.Abs(draw - lastTick) >= 0.1f)
                {
                    lastTick = draw;
                    var right = InputDevices.GetDeviceAtXRNode(XRNode.RightHand);
                    if (right.isValid) right.SendHapticImpulse(0u, 0.15f + 0.35f * draw, 0.02f);
                }

                if (grab < releaseThreshold)
                {
                    if (draw >= minFireDraw && canShoot) Fire(draw);
                    holding = false; draw = 0f;
                }
            }

            CurrentDraw = holding ? draw : 0f;
            UpdateVisuals(CurrentDraw);
            if (showAim && holding && draw >= minFireDraw) UpdateAim(Mathf.Lerp(minSpeed, maxSpeed, draw)); else SetAimVisible(false);
        }

        void Fire(float strength)
        {
            nextAllowed = Time.time + cooldown;
            if (arrowPrefab == null) return;
            Vector3 dir = transform.forward;
            Vector3 start = transform.position + dir * 0.1f;
            var arrow = Instantiate(arrowPrefab, start, Quaternion.LookRotation(dir));
            arrow.Launch(dir * Mathf.Lerp(minSpeed, maxSpeed, strength), ownerRoot, strength);
            Debug.Log("[Bow] fired, draw " + strength.ToString("0.00"));

            if (audioSource != null && releaseClip != null) audioSource.PlayOneShot(releaseClip);
            if (audioSource != null && flightClip != null) audioSource.PlayOneShot(flightClip, flightVolume * Mathf.Lerp(0.5f, 1f, strength));
            var device = InputDevices.GetDeviceAtXRNode(XRNode.LeftHand);
            if (device.isValid) device.SendHapticImpulse(0u, Mathf.Lerp(0.3f, 1f, strength), 0.09f);
        }

        void UpdateVisuals(float d)
        {
            if (nock != null) nock.localPosition = nockRest + Vector3.back * (maxDrawDistance * d);
            if (stringLine != null && stringTop != null && stringBottom != null && nock != null)
            {
                stringLine.SetPosition(0, stringTop.position);
                stringLine.SetPosition(1, nock.position);
                stringLine.SetPosition(2, stringBottom.position);
            }
            if (nockedArrow != null) nockedArrow.gameObject.SetActive(holding && Time.time >= nextAllowed);
        }

        void UpdateAim(float speed)
        {
            if (aimLine == null) return;
            Vector3 p = transform.position + transform.forward * 0.1f;
            Vector3 vel = transform.forward * speed;
            Vector3 g = Vector3.down * 9.81f;
            int count = 0;
            bool hitSomething = false;
            Vector3 hitPoint = Vector3.zero;
            aimBuffer[count++] = p;
            while (count < aimBuffer.Length)
            {
                Vector3 step = vel * aimStep + 0.5f * g * aimStep * aimStep;
                vel += g * aimStep;
                if (Physics.Raycast(p, step.normalized, out var hit, step.magnitude, Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore)
                    && (ownerRoot == null || !hit.collider.transform.IsChildOf(ownerRoot)))
                {
                    aimBuffer[count++] = hit.point; hitSomething = true; hitPoint = hit.point; break;
                }
                p += step;
                aimBuffer[count++] = p;
            }
            aimLine.enabled = true;
            aimLine.positionCount = count;
            for (int i = 0; i < count; i++) aimLine.SetPosition(i, aimBuffer[i]);
            if (aimDot != null)
            {
                aimDot.gameObject.SetActive(hitSomething);
                if (hitSomething)
                {
                    aimDot.position = hitPoint;
                    float dist = Vector3.Distance(transform.position, hitPoint);
                    aimDot.localScale = Vector3.one * Mathf.Clamp(dist * 0.012f, 0.03f, 0.6f);
                }
            }
        }

        void SetAimVisible(bool on)
        {
            if (aimLine != null) aimLine.enabled = on;
            if (aimDot != null && !on) aimDot.gameObject.SetActive(false);
        }
    }
}
