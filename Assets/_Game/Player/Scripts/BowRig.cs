using UnityEngine;

namespace BowGame
{
    // Bends the rigged bow model with the draw amount by posing it from its own animation clip,
    // and keeps the nocked arrow on the model's string.
    public class BowRig : MonoBehaviour
    {
        public AnimationClip clip;
        public GameObject modelRoot;      // the bow model the clip animates
        public Transform stringBone;      // middle of the model's string
        public Transform nock;            // the Bow's nock marker (the nocked arrow hangs off it)
        public float restTime;            // clip time where the bow is relaxed
        public float fullTime;            // clip time where the bow is fully drawn

        void LateUpdate()
        {
            if (clip != null && modelRoot != null)
                clip.SampleAnimation(modelRoot, Mathf.Lerp(restTime, fullTime, Mathf.Clamp01(Bow.CurrentDraw)));
            if (nock != null && stringBone != null) nock.position = stringBone.position;
        }
    }
}
