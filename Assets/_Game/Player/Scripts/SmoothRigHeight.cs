using Unity.XR.CoreUtils;
using UnityEngine;

namespace BowGame
{
    // Takes the bumps out of walking over uneven terrain.
    // The body (CharacterController) still follows the ground exactly; only the view (and the hands with it)
    // eases up and down instead of jumping, by nudging the rig's Camera Offset to cancel the sudden part of
    // each height change. Teleports and big drops are passed through straight away.
    [DefaultExecutionOrder(10000)]   // after locomotion has moved the rig this frame
    public class SmoothRigHeight : MonoBehaviour
    {
        [Tooltip("How quickly the view catches up with the ground (seconds). Higher = smoother, but floatier.")]
        public float smoothTime = 0.12f;
        [Tooltip("Height changes bigger than this in one frame (teleports, falls) are not smoothed.")]
        public float snapDistance = 0.6f;

        XROrigin origin;
        Transform offsetT;
        float baseLocalY, lastWrittenY, smoothY, velocity;
        bool ready;

        void Start()
        {
            origin = FindAnyObjectByType<XROrigin>();
            if (origin == null || origin.CameraFloorOffsetObject == null) { enabled = false; return; }
            offsetT = origin.CameraFloorOffsetObject.transform;
        }

        void LateUpdate()
        {
            if (offsetT == null) return;
            float rigY = origin.transform.position.y;

            // XROrigin (re)sets the offset height when tracking changes; take that as the new base.
            if (!ready || !Mathf.Approximately(offsetT.localPosition.y, lastWrittenY))
            {
                baseLocalY = offsetT.localPosition.y;
                smoothY = rigY; velocity = 0f; ready = true;
            }

            if (Mathf.Abs(rigY - smoothY) > snapDistance) { smoothY = rigY; velocity = 0f; }
            else smoothY = Mathf.SmoothDamp(smoothY, rigY, ref velocity, smoothTime);

            var p = offsetT.localPosition;
            p.y = baseLocalY + (smoothY - rigY) / Mathf.Max(0.0001f, origin.transform.lossyScale.y);
            offsetT.localPosition = p;
            lastWrittenY = p.y;
        }

        void OnDisable()
        {
            if (offsetT != null && ready) { var p = offsetT.localPosition; p.y = baseLocalY; offsetT.localPosition = p; }
            ready = false;
        }
    }
}
