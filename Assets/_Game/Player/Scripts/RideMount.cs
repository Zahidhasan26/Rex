using Unity.XR.CoreUtils;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace BowGame
{
    // Cosmetic "ride": the player's eyes are lifted onto the back of a big animal, and the animal's
    // legs move while the rider moves. It does NOT change how the player moves: the VR template's
    // locomotion stays in charge, this only follows it.
    //
    // No scene needs editing. Every time a scene loads, if it has an XR Origin and there is a prefab named
    // "Mount_Gorgosaurus" in a Resources folder, it is spawned and attached automatically.
    // To switch the ride off: untick Ride Enabled on that prefab (or remove the prefab).
    //
    // The mount prefab must be VISUAL ONLY: the model plus an Animator. Not the enemy prefab, no
    // NavMeshAgent. Colliders are removed at runtime so arrows never hit the animal being ridden.
    [DisallowMultipleComponent]
    public class RideMount : MonoBehaviour
    {
        public const string ResourceName = "Mount_Gorgosaurus";

        [Header("On / off")]
        public bool rideEnabled = true;

        [Header("Seat")]
        [Tooltip("Metres the rider is lifted above the ground (the animal's back height).")]
        public float seatHeight = 3.2f;

        [Header("Placement (relative to the rider, in the animal's own heading)")]
        [Tooltip("Where the animal's pivot sits compared with the rider. Z negative = animal body behind the rider's feet. Y moves it up or down.")]
        public Vector3 mountOffset = new Vector3(0f, 0f, -1.5f);
        [Tooltip("Turns the model if its forward axis is not +Z.")]
        public float modelYawOffset = 0f;
        [Tooltip("Degrees per second the animal turns toward the direction it is moving.")]
        public float turnSpeed = 90f;
        [Tooltip("Below this speed (m/s) the animal counts as standing still.")]
        public float minMoveSpeed = 0.15f;

        [Header("Walk animation (plays the model's clip while the rider moves)")]
        public Animator animator;
        [Tooltip("Rider speed (m/s) at which the clip looks right at normal speed.")]
        public float animReferenceSpeed = 3f;
        public float animMinSpeed = 0.5f;
        public float animMaxSpeed = 2f;
        [Tooltip("Animator speed while standing still. 0 freezes the pose.")]
        public float idleAnimSpeed = 0f;

        Transform rig;            // the XR Origin root (moved by the template's locomotion)
        Transform cameraOffset;   // parent of the camera and controllers
        bool attached;
        bool visible = true;

        float heading;            // the animal's own yaw, in degrees
        Vector3 lastRigPos;
        float smoothSpeed;

        float baseOffsetY;        // camera offset height without our lift
        float lastAppliedY;
        bool applied;

        // ---------- automatic setup ----------

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        static void Init()
        {
            // Also runs after a restart that reloads the scene (the old mount is destroyed with it).
            SceneManager.sceneLoaded -= OnSceneLoaded;
            SceneManager.sceneLoaded += OnSceneLoaded;
            AutoSpawn();
        }

        static void OnSceneLoaded(Scene scene, LoadSceneMode mode) => AutoSpawn();

        static void AutoSpawn()
        {
            if (FindFirstObjectByType<RideMount>() != null) return;

            var origin = FindFirstObjectByType<XROrigin>();
            if (origin == null || origin.Camera == null) return;      // test scenes without a VR rig: no ride

            var prefab = Resources.Load<GameObject>(ResourceName);
            if (prefab == null) return;                               // no prefab = feature off

            var go = Instantiate(prefab);
            go.name = ResourceName;
            var mount = go.GetComponent<RideMount>();
            if (mount == null)
            {
                Debug.LogWarning("RideMount: the prefab '" + ResourceName + "' has no RideMount component. Not spawned.");
                Destroy(go);
                return;
            }
            mount.Attach(origin);
        }

        void Attach(XROrigin origin)
        {
            // An enemy dinosaur prefab must never be used as the mount (every enemy has a NavMeshAgent).
            if (GetComponentInChildren<UnityEngine.AI.NavMeshAgent>(true) != null)
            {
                Debug.LogError("RideMount: the mount prefab contains a NavMeshAgent, so it is probably the enemy prefab. Use the plain model. Not spawned.");
                Destroy(gameObject);
                return;
            }

            rig = origin.transform;
            cameraOffset = origin.Camera.transform.parent;

            foreach (var c in GetComponentsInChildren<Collider>(true)) Destroy(c);
            foreach (var rb in GetComponentsInChildren<Rigidbody>(true)) Destroy(rb);
            if (animator == null) animator = GetComponentInChildren<Animator>();

            heading = origin.Camera.transform.eulerAngles.y;
            lastRigPos = rig.position;
            attached = true;
        }

        // ---------- every frame, after the template has moved the rig ----------

        void LateUpdate()
        {
            if (!attached) return;
            if (rig == null) { Destroy(gameObject); return; }

            if (!rideEnabled)
            {
                RestoreSeat();
                SetVisible(false);
                lastRigPos = rig.position;
                return;
            }
            SetVisible(true);

            float dt = Mathf.Max(Time.deltaTime, 0.0001f);

            // How fast and which way the rider is moving (horizontal only).
            Vector3 pos = rig.position;
            Vector3 delta = pos - lastRigPos;
            lastRigPos = pos;
            delta.y = 0f;
            float speed = delta.magnitude / dt;
            if (speed > 15f) speed = 0f;                 // ignore jumps from respawns and teleports
            smoothSpeed = Mathf.Lerp(smoothSpeed, speed, 1f - Mathf.Exp(-8f * dt));

            if (speed > minMoveSpeed)
            {
                float target = Mathf.Atan2(delta.x, delta.z) * Mathf.Rad2Deg;
                heading = Mathf.MoveTowardsAngle(heading, target, turnSpeed * dt);
            }

            // Animal sits under and around the rider, with its own heading (snap turns don't spin it).
            Quaternion yaw = Quaternion.Euler(0f, heading, 0f);
            transform.SetPositionAndRotation(pos + yaw * mountOffset, yaw * Quaternion.Euler(0f, modelYawOffset, 0f));

            ApplySeat();

            if (animator != null)
            {
                animator.speed = smoothSpeed > minMoveSpeed
                    ? Mathf.Clamp(smoothSpeed / animReferenceSpeed, animMinSpeed, animMaxSpeed)
                    : idleAnimSpeed;
            }
        }

        // Lift the camera offset (head and hands) so the rider sits on the animal's back.
        void ApplySeat()
        {
            if (cameraOffset == null) return;
            Vector3 p = cameraOffset.localPosition;
            // If something else (the XR Origin) changed the height, remember its value as the new base.
            if (!applied || Mathf.Abs(p.y - lastAppliedY) > 0.001f) baseOffsetY = p.y;
            lastAppliedY = baseOffsetY + seatHeight;
            p.y = lastAppliedY;
            cameraOffset.localPosition = p;
            applied = true;
        }

        void RestoreSeat()
        {
            if (!applied || cameraOffset == null) return;
            Vector3 p = cameraOffset.localPosition;
            p.y = baseOffsetY;
            cameraOffset.localPosition = p;
            applied = false;
        }

        void SetVisible(bool value)
        {
            if (visible == value) return;
            visible = value;
            foreach (var r in GetComponentsInChildren<Renderer>(true)) r.enabled = value;
        }

        void OnDestroy()
        {
            RestoreSeat();
        }
    }
}
