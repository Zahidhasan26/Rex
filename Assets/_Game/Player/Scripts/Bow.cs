using UnityEngine;
using UnityEngine.XR;

namespace BowGame
{
    // The bow. Sits on the left controller; its forward (blue) axis is where arrows go.
    // Draw strength comes from EmgInput.Value. Relaxing after a draw fires the arrow.
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

        [Header("Feel")]
        public float maxDrawDistance = 0.45f;  // how far the string pulls back at full draw (metres)
        public float drawThreshold = 0.2f;     // flex above this starts a draw
        public float minFireDraw = 0.3f;       // weaker draws than this are cancelled, not fired
        public float releaseRatio = 0.45f;     // fire when flex falls below this fraction of the peak
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

        bool drawing;
        float peak;
        float nextAllowed;
        Vector3 nockRest;
        Vector3[] aimBuffer;

        void Start()
        {
            if (nock != null) nockRest = nock.localPosition;
            aimBuffer = new Vector3[Mathf.Max(2, aimPoints)];
            if (stringLine != null) stringLine.positionCount = 3;
            SetAimVisible(false);
        }

        void Update()
        {
            float v = EmgInput.Value;
            bool canShoot = GameRefs.State != GameState.Dead && Time.time >= nextAllowed;

            if (!drawing)
            {
                if (canShoot && v > drawThreshold)
                {
                    drawing = true; peak = v;
                    if (audioSource != null && drawClip != null) audioSource.PlayOneShot(drawClip);
                }
            }
            else
            {
                if (v > peak) peak = v;
                if (v < Mathf.Max(0.08f, peak * releaseRatio))
                {
                    if (peak >= minFireDraw) Fire(peak);
                    drawing = false; peak = 0f;
                }
            }

            float shown = drawing ? Mathf.Max(v, peak * 0.85f) : 0f;
            CurrentDraw = shown;
            UpdateVisuals(shown);
            if (showAim && drawing) UpdateAim(Mathf.Lerp(minSpeed, maxSpeed, peak)); else SetAimVisible(false);
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
            var device = InputDevices.GetDeviceAtXRNode(XRNode.LeftHand);
            if (device.isValid) device.SendHapticImpulse(0u, Mathf.Lerp(0.3f, 1f, strength), 0.09f);
        }

        void UpdateVisuals(float draw)
        {
            if (nock != null) nock.localPosition = nockRest + Vector3.back * (maxDrawDistance * draw);
            if (stringLine != null && stringTop != null && stringBottom != null && nock != null)
            {
                stringLine.SetPosition(0, stringTop.position);
                stringLine.SetPosition(1, nock.position);
                stringLine.SetPosition(2, stringBottom.position);
            }
            if (nockedArrow != null) nockedArrow.gameObject.SetActive(drawing && Time.time >= nextAllowed);
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
                    float d = Vector3.Distance(transform.position, hitPoint);
                    aimDot.localScale = Vector3.one * Mathf.Clamp(d * 0.012f, 0.03f, 0.6f);
                }
            }
        }

        void SetAimVisible(bool on)
        {
            if (aimLine != null) aimLine.enabled = on;
            if (aimDot != null && !on) aimDot.gameObject.SetActive(false);
        }

        void OnDisable() { CurrentDraw = 0f; }
    }
}
