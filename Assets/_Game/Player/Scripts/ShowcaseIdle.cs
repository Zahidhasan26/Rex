using UnityEngine;

namespace BowGame
{
    // Showcase helper: loops one animation clip on a model without needing an Animator setup.
    public class ShowcaseIdle : MonoBehaviour
    {
        public AnimationClip clip;
        public float speed = 1f;

        void LateUpdate()
        {
            if (clip == null || clip.length <= 0f) return;
            clip.SampleAnimation(gameObject, (Time.time * speed) % clip.length);
        }
    }
}
