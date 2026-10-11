using UnityEngine;

namespace BowGame
{
    // Looping background ambience for a scene. Fades in on start.
    [RequireComponent(typeof(AudioSource))]
    public class AmbientAudio : MonoBehaviour
    {
        [Range(0f, 1f)] public float volume = 0.35f;
        public float fadeInTime = 3f;

        AudioSource src;
        float t;

        void Awake()
        {
            src = GetComponent<AudioSource>();
            src.loop = true;
            src.spatialBlend = 0f;
            src.volume = 0f;
        }

        void Start()
        {
            if (src.clip != null && !src.isPlaying) src.Play();
        }

        void Update()
        {
            if (t >= fadeInTime) return;
            t += Time.deltaTime;
            src.volume = volume * Mathf.Clamp01(fadeInTime > 0f ? t / fadeInTime : 1f);
        }
    }
}
