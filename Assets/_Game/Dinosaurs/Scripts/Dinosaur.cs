using System.Collections;
using UnityEngine;
using UnityEngine.AI;

namespace BowGame
{
    // Shared dinosaur brain: stalk the player in a natural, slightly random way, bite in range,
    // take arrow hits, die. Maip and Gorgosaurus inherit from this and only change the numbers.
    // Works with or without an Animator, so it can be tested on a capsule stand-in.
    [RequireComponent(typeof(NavMeshAgent))]
    public class Dinosaur : MonoBehaviour, IDamageable
    {
        [Header("Health")]
        [Tooltip("Body hits needed to kill. Head hits always kill instantly.")]
        public int bodyHitsToKill = 3;

        [Header("Leg hits slow the dinosaur")]
        [Range(0.05f, 1f)] public float legSlowMultiplier = 0.4f;
        public float legSlowDuration = 4f;

        [Header("Movement")]
        public float moveSpeed = 4f;
        public float turnSpeed = 360f;               // degrees per second when facing the player to attack
        [Tooltip("Random range for how often the path is recalculated, in seconds.")]
        public Vector2 repathInterval = new Vector2(0.3f, 0.7f);

        [Header("Natural approach")]
        [Tooltip("Each dinosaur gets its own speed, up to this fraction faster or slower.")]
        [Range(0f, 0.5f)] public float speedVariance = 0.15f;
        [Tooltip("How much speed drifts up and down over time.")]
        [Range(0f, 0.5f)] public float speedWobble = 0.2f;
        [Tooltip("Each dinosaur circles in from one side by up to this many degrees, so they don't all come straight at you.")]
        public Vector2 flankAngle = new Vector2(10f, 35f);
        [Tooltip("Sideways weaving distance in metres while far away.")]
        public float weaveAmplitude = 5f;
        [Tooltip("How fast the weaving changes direction (lower is lazier).")]
        public float weaveFrequency = 0.12f;
        [Tooltip("How far ahead each move target is placed, in metres.")]
        public float lookAhead = 8f;
        [Tooltip("Inside this distance the dinosaur stops circling and goes straight for the player.")]
        public float directApproachDistance = 9f;
        [Tooltip("Circling and weaving fade in over this many metres beyond the direct-approach distance.")]
        public float wanderFadeDistance = 12f;

        [Header("Pauses (sniffing, looking around)")]
        [Tooltip("Chance per second of stopping briefly while still far away.")]
        [Range(0f, 1f)] public float pauseChancePerSecond = 0.06f;
        public Vector2 pauseDuration = new Vector2(0.6f, 1.8f);

        [Header("Charge")]
        [Tooltip("Speeds up when this close to the player.")]
        public float chargeDistance = 10f;
        public float chargeSpeedMultiplier = 1.4f;

        [Header("Attack (bite)")]
        [Tooltip("Starts a bite when the player is this close (metres, flat distance from the dinosaur's centre).")]
        public float attackRange = 2.5f;
        public float attackCooldown = 1.5f;
        public float damage = 10f;
        [Tooltip("Seconds from the start of the attack to the moment the bite lands (match the attack animation).")]
        public float biteDelay = 0.35f;
        [Tooltip("Seconds after the bite before it can move again.")]
        public float recoverTime = 0.4f;
        [Tooltip("Player must still be within attackRange times this when the bite lands, or it misses.")]
        public float biteReachMultiplier = 1.2f;

        [Header("Death")]
        [Tooltip("Seconds the body stays before it is removed.")]
        public float corpseTime = 4f;

        [Header("Animation (optional)")]
        public Animator animator;
        [Tooltip("Play the animation faster or slower to match how fast the dinosaur is moving.")]
        public bool syncAnimationToSpeed = true;
        public Vector2 animationSpeedRange = new Vector2(0.35f, 1.4f);
        public string speedParam = "Speed";          // float
        public string attackTrigger = "Attack";      // trigger
        public string hitTrigger = "Hit";            // trigger
        public string dieTrigger = "Die";            // trigger

        protected NavMeshAgent agent;
        protected bool isDead;
        protected bool isAttacking;
        protected float nextAttackTime;

        int bodyHits;
        float slowUntil;
        float nextRepath;
        float pausedUntil;
        float personalSpeed = 1f;
        float flank;
        float noiseSeed;

        public bool IsDead => isDead;

        // Events for other components on the dinosaur (sound, effects). Nothing outside the dinosaur uses these.
        public event System.Action Spawned;
        public event System.Action Paused;
        public event System.Action ChargeStarted;
        public event System.Action AttackStarted;
        public event System.Action<HitZone, Vector3> Hit;
        public event System.Action Died;
        public event System.Action Collapsed;

        bool charging;

        protected virtual void Awake()
        {
            agent = GetComponent<NavMeshAgent>();
            if (animator == null) animator = GetComponentInChildren<Animator>();

            // Give every dinosaur its own personality so a group doesn't move in lockstep.
            personalSpeed = 1f + Random.Range(-speedVariance, speedVariance);
            flank = Random.Range(flankAngle.x, flankAngle.y) * (Random.value < 0.5f ? -1f : 1f);
            noiseSeed = Random.Range(0f, 1000f);
            nextAttackTime = Time.time + Random.Range(0f, attackCooldown);
        }

        protected virtual void Start()
        {
            agent.speed = moveSpeed * personalSpeed;
            Spawned?.Invoke();
        }

        protected virtual void Update()
        {
            if (isDead) return;

            UpdateAnimation();

            Transform player = GameRefs.PlayerHead;
            bool roundOver = GameRefs.State == GameState.Dead || GameRefs.State == GameState.ActOneComplete;
            if (player == null || roundOver || !agent.isOnNavMesh)
            {
                if (agent.isOnNavMesh) agent.isStopped = true;
                return;
            }

            float dist = FlatDistance(transform.position, player.position);

            if (isAttacking)
            {
                agent.isStopped = true;
                FaceTowards(player.position);
                return;
            }

            if (dist <= attackRange && CanAttack(player))
            {
                agent.isStopped = true;
                agent.velocity = Vector3.zero;   // plant its feet instead of sliding through the player
                FaceTowards(player.position);
                if (Time.time >= nextAttackTime)
                {
                    nextAttackTime = Time.time + attackCooldown * Random.Range(0.85f, 1.2f);
                    StartCoroutine(AttackRoutine(player));
                }
                return;
            }

            // Occasional pause while still far away: stop, then carry on.
            float wander01 = Mathf.InverseLerp(directApproachDistance, directApproachDistance + wanderFadeDistance, dist);
            if (Time.time < pausedUntil)
            {
                agent.isStopped = true;
                FaceTowards(player.position, 0.3f);
                return;
            }
            if (wander01 >= 1f && Random.value < pauseChancePerSecond * Time.deltaTime)
            {
                pausedUntil = Time.time + Random.Range(pauseDuration.x, pauseDuration.y);
                Paused?.Invoke();
                return;
            }

            agent.isStopped = false;
            if (!charging && dist <= chargeDistance) { charging = true; ChargeStarted?.Invoke(); }
            else if (charging && dist > chargeDistance * 1.5f) charging = false;
            agent.speed = CurrentSpeed(dist);
            agent.autoBraking = wander01 <= 0f;   // brake on the final run-in so it stops at biting range
            agent.stoppingDistance = dist <= attackRange ? 1f : attackRange * 0.8f;

            if (Time.time >= nextRepath)
            {
                nextRepath = Time.time + Random.Range(repathInterval.x, repathInterval.y);
                agent.SetDestination(PickMoveTarget(player.position, dist, wander01));
            }
        }

        // Where to walk next. Far away: circle in from one side while weaving.
        // Close in: straight at the player so it can actually reach and bite.
        Vector3 PickMoveTarget(Vector3 playerPos, float dist, float wander01)
        {
            if (wander01 <= 0f) return playerPos;

            Vector3 toPlayer = playerPos - transform.position;
            toPlayer.y = 0f;
            Vector3 dir = toPlayer.sqrMagnitude > 0.001f ? toPlayer.normalized : transform.forward;

            Vector3 approach = Quaternion.Euler(0f, flank * wander01, 0f) * dir;
            Vector3 side = Vector3.Cross(Vector3.up, approach);
            float weave = (Mathf.PerlinNoise(noiseSeed, Time.time * weaveFrequency) * 2f - 1f) * weaveAmplitude * wander01;

            Vector3 target = transform.position + approach * Mathf.Min(dist, lookAhead) + side * weave;
            if (NavMesh.SamplePosition(target, out NavMeshHit hit, 4f, NavMesh.AllAreas)) return hit.position;
            return playerPos;
        }

        float CurrentSpeed(float dist)
        {
            float wobble = 1f + (Mathf.PerlinNoise(noiseSeed + 50f, Time.time * 0.4f) * 2f - 1f) * speedWobble;
            float charge = dist <= chargeDistance ? chargeSpeedMultiplier : 1f;
            float slow = Time.time < slowUntil ? legSlowMultiplier : 1f;
            return moveSpeed * personalSpeed * wobble * charge * slow;
        }

        void UpdateAnimation()
        {
            float v = agent.enabled ? agent.velocity.magnitude : 0f;
            SetAnimFloat(speedParam, v);
            if (animator != null && syncAnimationToSpeed && !HasParam(speedParam))
            {
                float target = Mathf.Clamp(v / Mathf.Max(0.1f, moveSpeed), animationSpeedRange.x, animationSpeedRange.y);
                animator.speed = Mathf.MoveTowards(animator.speed, target, Time.deltaTime * 2f);
            }
        }

        // Override for extra conditions.
        protected virtual bool CanAttack(Transform player) => true;

        // Default attack: a bite that lands after biteDelay if the player is still in reach.
        protected virtual IEnumerator AttackRoutine(Transform player)
        {
            isAttacking = true;
            SetAnimTrigger(attackTrigger);
            AttackStarted?.Invoke();

            yield return new WaitForSeconds(biteDelay);

            if (!isDead && player != null &&
                FlatDistance(transform.position, player.position) <= attackRange * biteReachMultiplier)
            {
                GameEvents.RaisePlayerHit(damage, transform.position);
            }

            yield return new WaitForSeconds(recoverTime);
            isAttacking = false;
        }

        // ---------- IDamageable (called by Zahid's arrow) ----------

        public void TakeHit(HitZone zone, float drawStrength01, Vector3 hitPoint)
        {
            if (isDead) return;

            pausedUntil = 0f;   // getting shot snaps it out of a pause
            Hit?.Invoke(zone, hitPoint);
            switch (zone)
            {
                case HitZone.Head:
                    Die();
                    return;
                case HitZone.Body:
                    bodyHits++;
                    if (bodyHits >= bodyHitsToKill) { Die(); return; }
                    break;
                case HitZone.Legs:
                    slowUntil = Time.time + legSlowDuration;
                    break;
            }
            SetAnimTrigger(hitTrigger);
        }

        protected virtual void Die()
        {
            if (isDead) return;
            isDead = true;
            StopAllCoroutines();
            isAttacking = false;

            if (agent.isOnNavMesh) agent.isStopped = true;
            agent.enabled = false;
            foreach (var c in GetComponentsInChildren<Collider>()) c.enabled = false;

            if (HasParam(dieTrigger)) SetAnimTrigger(dieTrigger);
            else
            {
                if (animator != null) animator.speed = 0f;   // freeze the pose
                StartCoroutine(FallOver());                  // stand-in without a death animation
            }

            GameEvents.RaiseDinoDied(gameObject);
            Died?.Invoke();
            Destroy(gameObject, corpseTime);
        }

        IEnumerator FallOver()
        {
            Quaternion from = transform.rotation;
            Quaternion to = from * Quaternion.Euler(0f, 0f, Random.value < 0.5f ? 90f : -90f);
            for (float t = 0f; t < 1f; t += Time.deltaTime * 2f)
            {
                transform.rotation = Quaternion.Slerp(from, to, t);
                yield return null;
            }
            transform.rotation = to;
            Collapsed?.Invoke();
        }

        // ---------- helpers ----------

        protected void FaceTowards(Vector3 target, float speedScale = 1f)
        {
            Vector3 dir = target - transform.position;
            dir.y = 0f;
            if (dir.sqrMagnitude < 0.0001f) return;
            transform.rotation = Quaternion.RotateTowards(transform.rotation, Quaternion.LookRotation(dir), turnSpeed * speedScale * Time.deltaTime);
        }

        protected static float FlatDistance(Vector3 a, Vector3 b)
        {
            a.y = 0f; b.y = 0f;
            return Vector3.Distance(a, b);
        }

        protected bool HasParam(string name)
        {
            if (animator == null || animator.runtimeAnimatorController == null || string.IsNullOrEmpty(name)) return false;
            foreach (var p in animator.parameters) if (p.name == name) return true;
            return false;
        }

        protected void SetAnimTrigger(string name)
        {
            if (HasParam(name)) animator.SetTrigger(name);
        }

        protected void SetAnimFloat(string name, float value)
        {
            if (HasParam(name)) animator.SetFloat(name, value);
        }
    }
}
