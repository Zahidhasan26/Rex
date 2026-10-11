using UnityEngine;
using UnityEngine.AI;

namespace BowGame
{
    // Sound for one dinosaur. Put it on the dinosaur's root next to the Maip / Gorgosaurus script.
    // Listens to the Dinosaur events; every list can hold several clips and one is picked at random.
    [RequireComponent(typeof(Dinosaur))]
    public class DinoAudio : MonoBehaviour
    {
        [Header("Voice (mouth)")]
        public AudioSource voice;
        public AudioClip[] spawnCalls;
        public AudioClip[] pauseGrowls;
        public AudioClip[] chargeRoars;
        public AudioClip[] attackSounds;
        public AudioClip[] hurtSounds;
        public AudioClip[] deathSounds;

        [Header("Body")]
        public AudioSource body;
        public AudioClip[] footsteps;
        public AudioClip[] collapseSounds;
        [Tooltip("Metres covered per footstep at normal speed (bigger animals take longer strides).")]
        public float strideLength = 2f;
        [Range(0f, 1f)] public float footstepVolume = 0.6f;

        [Header("Arrow impacts (played where the arrow hit)")]
        public AudioClip[] bodyHitSounds;
        public AudioClip[] headHitSounds;
        [Range(0f, 1f)] public float impactVolume = 0.9f;

        [Header("Character")]
        [Tooltip("Base pitch. Below 1 sounds bigger and deeper, above 1 smaller and faster.")]
        public float pitch = 1f;
        [Range(0f, 0.3f)] public float pitchVariance = 0.08f;
        [Tooltip("Chance a spawn call actually plays, so a big wave doesn't become a wall of roars.")]
        [Range(0f, 1f)] public float spawnCallChance = 0.6f;

        Dinosaur dino;
        NavMeshAgent agent;
        float stepDistance;

        void Awake()
        {
            dino = GetComponent<Dinosaur>();
            agent = GetComponent<NavMeshAgent>();
        }

        void OnEnable()
        {
            dino.Spawned += OnSpawned;
            dino.Paused += OnPaused;
            dino.ChargeStarted += OnCharge;
            dino.AttackStarted += OnAttack;
            dino.Hit += OnHit;
            dino.Died += OnDied;
            dino.Collapsed += OnCollapsed;
        }

        void OnDisable()
        {
            dino.Spawned -= OnSpawned;
            dino.Paused -= OnPaused;
            dino.ChargeStarted -= OnCharge;
            dino.AttackStarted -= OnAttack;
            dino.Hit -= OnHit;
            dino.Died -= OnDied;
            dino.Collapsed -= OnCollapsed;
        }

        void Update()
        {
            if (dino.IsDead || agent == null || !agent.enabled) return;
            float speed = agent.velocity.magnitude;
            if (speed < 0.3f) return;
            stepDistance += speed * Time.deltaTime;
            if (stepDistance >= strideLength)
            {
                stepDistance = 0f;
                Play(body, footsteps, footstepVolume * Mathf.Clamp01(0.5f + speed / Mathf.Max(1f, dino.moveSpeed) * 0.5f));
            }
        }

        void OnSpawned() { if (Random.value < spawnCallChance) Play(voice, spawnCalls, 1f); }
        void OnPaused() => Play(voice, pauseGrowls, 0.8f);
        void OnCharge() => Play(voice, chargeRoars, 1f, interrupt: true);
        void OnAttack() => Play(voice, attackSounds, 1f, interrupt: true);
        void OnDied() => Play(voice, deathSounds, 1f, interrupt: true);
        void OnCollapsed() => Play(body, collapseSounds, 1f);

        void OnHit(HitZone zone, Vector3 point)
        {
            var impacts = zone == HitZone.Head ? headHitSounds : bodyHitSounds;
            var clip = Pick(impacts);
            if (clip != null) AudioSource.PlayClipAtPoint(clip, point, impactVolume);
            if (zone != HitZone.Head) Play(voice, hurtSounds, 1f, interrupt: true);   // head hits kill: the death sound plays instead
        }

        void Play(AudioSource src, AudioClip[] clips, float volume, bool interrupt = false)
        {
            if (src == null) return;
            var clip = Pick(clips);
            if (clip == null) return;
            if (interrupt) src.Stop();
            src.pitch = pitch * (1f + Random.Range(-pitchVariance, pitchVariance));
            src.PlayOneShot(clip, volume);
        }

        static AudioClip Pick(AudioClip[] clips)
        {
            if (clips == null || clips.Length == 0) return null;
            return clips[Random.Range(0, clips.Length)];
        }
    }
}
