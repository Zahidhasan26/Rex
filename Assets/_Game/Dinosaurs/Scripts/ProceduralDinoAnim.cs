using System.Collections.Generic;
using UnityEngine;

namespace BowGame
{
    // Adds the moves our dinosaur models are missing (death, attack, hit, walk) by rotating bones in code.
    // Runs in LateUpdate, so every rotation is added on top of whatever the Animator played this frame.
    // Put it on the same object as the Dinosaur script. Any bone slot can be left empty; that part is skipped.
    // All angles are in degrees around the dinosaur's own axes:
    //   pitch = nose down (+) / nose up (-), yaw = turn right (+) / left (-), roll = tip right (+) / left (-).
    [DisallowMultipleComponent]
    public class ProceduralDinoAnim : MonoBehaviour
    {
        [Header("Bones (leave empty to skip)")]
        public Transform head;
        public Transform neck;
        public Transform jaw;
        [Tooltip("Spine or body bone, near the middle of the back.")]
        public Transform spine;
        [Tooltip("Tail bones from the base to the tip.")]
        public List<Transform> tail = new List<Transform>();
        public Transform upperLegL;
        public Transform upperLegR;
        public Transform lowerLegL;
        public Transform lowerLegR;

        [Header("Death")]
        [Tooltip("How far the whole body tips onto its side.")]
        [Range(0f, 120f)] public float deathRollAngle = 85f;
        [Tooltip("Seconds to fall over.")]
        [Range(0.1f, 3f)] public float deathFallDuration = 0.7f;
        [Range(-60f, 60f)] public float deathNeckDroop = 25f;
        [Range(-60f, 60f)] public float deathHeadDroop = 20f;
        [Range(-60f, 60f)] public float deathJawOpen = 18f;
        [Tooltip("Pitch added to every tail bone so the tail goes limp.")]
        [Range(-30f, 30f)] public float deathTailDroop = 6f;
        [Tooltip("Knees fold as it collapses.")]
        [Range(-90f, 90f)] public float deathKneeFold = 35f;
        [Tooltip("Metres the body sinks into the ground just before it disappears.")]
        [Range(0f, 5f)] public float deathSinkDepth = 1.5f;
        [Tooltip("Seconds of sinking at the end (the Dinosaur's corpseTime decides when it disappears).")]
        [Range(0.1f, 3f)] public float deathSinkDuration = 1f;

        [Header("Attack (scary open mouth)")]
        [Range(0f, 80f)] public float attackJawOpen = 40f;
        [Tooltip("Negative lifts the head back before it snaps.")]
        [Range(-60f, 60f)] public float attackHeadPitch = -15f;
        [Range(-60f, 60f)] public float attackNeckPitch = -10f;
        [Tooltip("Small side-to-side head shake while the mouth is open.")]
        [Range(0f, 20f)] public float attackShakeAngle = 4f;
        [Range(0f, 40f)] public float attackShakeSpeed = 22f;
        [Tooltip("Seconds to open the mouth.")]
        [Range(0.02f, 1f)] public float attackOpenTime = 0.15f;
        [Tooltip("Seconds to close the mouth after the attack.")]
        [Range(0.02f, 1.5f)] public float attackCloseTime = 0.35f;
        [Tooltip("Keep the mouth open at least this long, even for a very short attack.")]
        [Range(0f, 2f)] public float attackMinHold = 0.4f;

        [Header("Hit flinch")]
        [Tooltip("Seconds for the whole flinch (snap back, then recover).")]
        [Range(0.1f, 1.5f)] public float hitDuration = 0.4f;
        [Tooltip("Seconds to reach the strongest point of the flinch.")]
        [Range(0.01f, 0.5f)] public float hitSnapTime = 0.06f;
        [Range(-45f, 45f)] public float hitSpinePitch = -8f;
        [Range(-60f, 60f)] public float hitNeckPitch = -12f;
        [Range(-60f, 60f)] public float hitHeadPitch = -15f;
        [Tooltip("Head snaps to one side (random left or right each hit).")]
        [Range(0f, 45f)] public float hitHeadSideways = 12f;
        [Range(0f, 60f)] public float hitJawOpen = 15f;
        [Tooltip("Side whip added to every tail bone.")]
        [Range(0f, 20f)] public float hitTailWhip = 5f;

        [Header("Walk (for a model with no walk animation, e.g. the Maip)")]
        public bool proceduralWalk = false;
        [Tooltip("Metres covered by one full step cycle (left + right). Bigger = slower, longer strides.")]
        [Range(0.5f, 10f)] public float strideLength = 3.2f;
        [Tooltip("Forward/back swing of the upper legs.")]
        [Range(0f, 60f)] public float legSwing = 28f;
        [Tooltip("Knee bend while the leg swings forward (foot lifts).")]
        [Range(-90f, 90f)] public float kneeLift = 35f;
        [Tooltip("Body rocks a little with each step.")]
        [Range(0f, 15f)] public float bodyBob = 3f;
        [Range(0f, 20f)] public float tailSway = 6f;
        [Range(0f, 20f)] public float headSway = 3f;
        [Tooltip("Speed (m/s) at which the walk is at full strength. Slower movement blends it in.")]
        [Range(0.1f, 10f)] public float fullWalkSpeed = 2.5f;

        [Header("Match the Animator's speed to movement (for a model that has a run clip, e.g. the Gorgosaurus)")]
        public bool matchAnimSpeedToMovement = false;
        [Tooltip("Movement speed (m/s) at which the clip looks right when played at normal speed.")]
        [Range(0.5f, 10f)] public float animReferenceSpeed = 3f;
        [Range(0f, 2f)] public float animMinSpeed = 0.6f;
        [Range(0.5f, 4f)] public float animMaxSpeed = 2f;

        Dinosaur dino;
        Animator animator;
        UnityEngine.AI.NavMeshAgent agent;
        float walkPhase;
        float walkWeight;

        float hitTime = -999f;
        float hitSide = 1f;

        float attackWeight;        // 0 = mouth shut, 1 = fully open
        float attackHoldUntil;

        bool dead;
        float deathTime;
        float corpseTime;
        float deathRollSign = 1f;
        Quaternion deathStartRotation;
        Vector3 deathStartPosition;

        // ---------- setup ----------

        void Awake()
        {
            dino = GetComponent<Dinosaur>();
            animator = GetComponentInChildren<Animator>();
            agent = GetComponent<UnityEngine.AI.NavMeshAgent>();
        }

        void OnEnable()
        {
            if (dino == null) return;
            dino.Died += OnDied;
            dino.AttackStarted += OnAttackStarted;
            dino.WasHit += OnWasHit;
        }

        void OnDisable()
        {
            if (dino == null) return;
            dino.Died -= OnDied;
            dino.AttackStarted -= OnAttackStarted;
            dino.WasHit -= OnWasHit;
        }

        void OnWasHit(HitZone zone)
        {
            hitTime = Time.time;
            hitSide = Random.value < 0.5f ? -1f : 1f;
        }

        void OnAttackStarted()
        {
            attackHoldUntil = Time.time + attackMinHold;
        }

        void OnDied()
        {
            dead = true;
            deathTime = Time.time;
            corpseTime = dino != null ? dino.corpseTime : 3f;
            deathRollSign = Random.value < 0.5f ? -1f : 1f;
            deathStartRotation = transform.rotation;
            deathStartPosition = transform.position;
            if (animator != null) animator.speed = 0f;   // freeze the clip; bone poses below still apply
        }

        // ---------- every frame, after the Animator ----------

        void LateUpdate()
        {
            BeginFrame();

            if (dead) { ApplyDeath(); return; }

            ApplyWalk();
            ApplyAttack();
            ApplyHit();
        }

        float MoveSpeed()
        {
            if (agent == null || !agent.enabled) return 0f;
            Vector3 v = agent.velocity; v.y = 0f;
            return v.magnitude;
        }

        void ApplyWalk()
        {
            float speed = MoveSpeed();

            if (matchAnimSpeedToMovement && animator != null)
                animator.speed = Mathf.Clamp(speed / animReferenceSpeed, animMinSpeed, animMaxSpeed);

            if (!proceduralWalk) return;

            float target = Mathf.Clamp01(speed / fullWalkSpeed);
            walkWeight = Mathf.MoveTowards(walkWeight, target, Time.deltaTime * 4f);
            if (walkWeight <= 0.001f) return;

            walkPhase += Time.deltaTime * speed / strideLength * Mathf.PI * 2f;
            if (walkPhase > Mathf.PI * 2f) walkPhase -= Mathf.PI * 2f;

            float w = walkWeight;
            float sL = Mathf.Sin(walkPhase);
            float sR = Mathf.Sin(walkPhase + Mathf.PI);
            // Upper legs swing opposite each other; the knee bends while that leg swings forward.
            Pitch(upperLegL, legSwing * sL * w);
            Pitch(upperLegR, legSwing * sR * w);
            Pitch(lowerLegL, kneeLift * Mathf.Max(0f, Mathf.Cos(walkPhase)) * w);
            Pitch(lowerLegR, kneeLift * Mathf.Max(0f, Mathf.Cos(walkPhase + Mathf.PI)) * w);

            // Body rocks side to side once per step, tail sways the other way, head stays steadier.
            Roll(spine, bodyBob * sL * w);
            for (int i = 0; i < tail.Count; i++) Yaw(tail[i], tailSway * sL * w * (i + 1) / tail.Count);
            Yaw(head, -headSway * sL * w);
        }

        void ApplyHit()
        {
            float t = Time.time - hitTime;
            if (t < 0f || t > hitDuration) return;

            // Snap back fast, then ease out over the rest of the duration.
            float w = t < hitSnapTime
                ? Ease(t / hitSnapTime)
                : 1f - Ease((t - hitSnapTime) / Mathf.Max(0.01f, hitDuration - hitSnapTime));

            Pitch(spine, hitSpinePitch * w);
            Pitch(neck, hitNeckPitch * w);
            Pitch(head, hitHeadPitch * w);
            Yaw(head, hitHeadSideways * hitSide * w);
            Pitch(jaw, hitJawOpen * w);
            for (int i = 0; i < tail.Count; i++) Yaw(tail[i], -hitTailWhip * hitSide * w);
        }

        void ApplyAttack()
        {
            bool open = (dino != null && dino.IsAttacking) || Time.time < attackHoldUntil;
            float rate = open ? 1f / attackOpenTime : -1f / attackCloseTime;
            attackWeight = Mathf.Clamp01(attackWeight + rate * Time.deltaTime);
            if (attackWeight <= 0f) return;

            float w = Ease(attackWeight);
            float shake = Mathf.Sin(Time.time * attackShakeSpeed) * attackShakeAngle * w;
            Pitch(neck, attackNeckPitch * w);
            Pitch(head, attackHeadPitch * w);
            Yaw(head, shake);
            Pitch(jaw, attackJawOpen * w);
        }

        void ApplyDeath()
        {
            float t = Time.time - deathTime;
            float fall = Ease(Mathf.Clamp01(t / deathFallDuration));

            // Whole body tips onto its side (the root has stopped moving: the NavMeshAgent is off).
            transform.rotation = deathStartRotation * Quaternion.Euler(0f, 0f, deathRollSign * deathRollAngle * fall);

            // Sink into the ground during the last seconds before the Dinosaur removes the body.
            float sinkStart = Mathf.Max(deathFallDuration, corpseTime - deathSinkDuration);
            float sink = Ease(Mathf.Clamp01((t - sinkStart) / deathSinkDuration));
            transform.position = deathStartPosition + Vector3.down * deathSinkDepth * sink;

            Pitch(neck, deathNeckDroop * fall);
            Pitch(head, deathHeadDroop * fall);
            Pitch(jaw, deathJawOpen * fall);
            for (int i = 0; i < tail.Count; i++) Pitch(tail[i], deathTailDroop * fall);
            Pitch(lowerLegL, deathKneeFold * fall);
            Pitch(lowerLegR, deathKneeFold * fall);
        }

        // ---------- bone helpers ----------
        // A bone the Animator does not touch keeps whatever we set last frame, so offsets would pile up.
        // Each frame we first put every bone back to the pose it had before our offset, then add the new offset.

        class BoneState { public Quaternion baseLocal; public Quaternion lastResult; public bool valid; }
        readonly Dictionary<Transform, BoneState> states = new Dictionary<Transform, BoneState>();

        void BeginFrame()
        {
            foreach (var kv in states)
            {
                Transform b = kv.Key;
                if (b == null) continue;
                BoneState s = kv.Value;
                // Same rotation as we left it: the Animator did not write this bone, so restore its base pose.
                if (s.valid && b.localRotation == s.lastResult) b.localRotation = s.baseLocal;
                s.baseLocal = b.localRotation;
                s.valid = false;
            }
        }

        // Adds a rotation around the dinosaur's own right axis (pitch), up axis (yaw) or forward axis (roll).
        void Pitch(Transform b, float degrees) { Rotate(b, transform.right, degrees); }
        void Yaw(Transform b, float degrees)   { Rotate(b, transform.up, degrees); }
        void Roll(Transform b, float degrees)  { Rotate(b, transform.forward, degrees); }

        void Rotate(Transform b, Vector3 axis, float degrees)
        {
            if (b == null || Mathf.Approximately(degrees, 0f)) return;
            BoneState s;
            if (!states.TryGetValue(b, out s))
            {
                s = new BoneState { baseLocal = b.localRotation };
                states[b] = s;
            }
            b.rotation = Quaternion.AngleAxis(degrees, axis) * b.rotation;
            s.lastResult = b.localRotation;
            s.valid = true;
        }

        static float Ease(float x) { return x * x * (3f - 2f * x); }
    }
}
