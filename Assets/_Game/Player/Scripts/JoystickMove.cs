using Unity.XR.CoreUtils;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.XR.Interaction.Toolkit.Locomotion.Movement;
using UnityEngine.XR.Interaction.Toolkit.Samples.StarterAssets;

namespace BowGame
{
    // Makes sure the LEFT JOYSTICK walks the player.
    // 1. Turns on smooth movement for the left controller and makes sure the move provider is active.
    // 2. Backup: if the stick is pushed but the rig doesn't move for half a second, this script
    //    moves the rig itself from then on (head-relative, with gravity), so walking always works.
    // GameFlow adds this at runtime; it can also be put on any object in the scene.
    public class JoystickMove : MonoBehaviour
    {
        public float fallbackSpeed = 2.5f;
        public float deadZone = 0.2f;

        XROrigin origin;
        CharacterController cc;
        ContinuousMoveProvider provider;
        InputAction stick;
        Vector3 lastPos;
        float stuckTime, fallY;
        bool providerWorks, fallback;

        void OnEnable()
        {
            stick = new InputAction("LeftStickMove", InputActionType.Value);
            stick.AddBinding("<XRController>{LeftHand}/primary2DAxis");
            stick.AddBinding("<XRController>{LeftHand}/thumbstick");
            stick.Enable();
        }

        void OnDisable() { stick?.Disable(); stick?.Dispose(); }

        void Start()
        {
            origin = FindAnyObjectByType<XROrigin>();
            if (origin == null) { enabled = false; return; }
            cc = origin.GetComponent<CharacterController>();

            foreach (var m in FindObjectsByType<ControllerInputActionManager>(FindObjectsInactive.Include, FindObjectsSortMode.None))
                if (m.name.Contains("Left")) m.smoothMotionEnabled = true;

            provider = origin.GetComponentInChildren<ContinuousMoveProvider>(true);
            if (provider != null)
            {
                if (!provider.gameObject.activeSelf) provider.gameObject.SetActive(true);
                provider.enabled = true;
                if (provider.moveSpeed > 0.1f) fallbackSpeed = provider.moveSpeed;
            }
            lastPos = origin.transform.position;
        }

        void LateUpdate()
        {
            if (origin == null) return;
            Vector2 s = stick.ReadValue<Vector2>();
            bool pushed = s.magnitude > deadZone;
            float dt = Time.deltaTime;

            Vector3 delta = origin.transform.position - lastPos; delta.y = 0f;
            if (!fallback && !providerWorks)
            {
                if (pushed && delta.magnitude < 1f)   // a bigger jump is a teleport, not walking
                {
                    if (delta.magnitude > 0.002f) providerWorks = true;
                    else if ((stuckTime += dt) > 0.5f)
                    {
                        fallback = true;
                        Debug.Log("JoystickMove: the move provider isn't moving the rig, using the backup mover.");
                    }
                }
                else stuckTime = 0f;
            }

            if (fallback && cc != null && cc.enabled)
            {
                var cam = origin.Camera.transform;
                Vector3 f = cam.forward; f.y = 0f; f.Normalize();
                Vector3 r = cam.right; r.y = 0f; r.Normalize();
                Vector3 move = pushed ? (f * s.y + r * s.x) * fallbackSpeed : Vector3.zero;
                fallY = cc.isGrounded ? -1f : fallY - 9.81f * dt;
                cc.Move((move + Vector3.up * fallY) * dt);
            }
            lastPos = origin.transform.position;
        }
    }
}
