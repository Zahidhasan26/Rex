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
        [Tooltip("Head hits needed to kill. 1 = any headshot kills. A head hit counts as bodyHitsToKill / headHitsToKill body hits.")]
        public int headHitsToKill = 1;
        [Tooltip("If above 0, one headshot counts as this many body shots instead (headHitsToKill is then ignored). Alpha: 2, with bodyHitsToKill 5 = 2 headshots + 1 body shot, or 5 body shots, and mixes add up.")]
        public float headShotWorth = 0f;
        [Tooltip("Alpha rule. If above 0: arrows can't kill it until it has bitten the player this many times; after that ONE arrow anywhere kills it.")]
        public int fragileAfterBites = 0;
        public static event System.Action<Dinosaur> BecameFragile;   // raised once when the rule above is met
        int bitesLanded;
        public bool IsFragile => fragileAfterBites > 0 && bitesLanded >= fragileAfterBites;

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

        [Header("Bite feel")]
        [Tooltip("The dinosaur's head bone. If set, the head is kept at least faceGap metres from the player so it never passes through them.")]
        public Transform mouth;
        public float faceGap = 0.45f;
        [Tooltip("Hold the bite for a moment if the player is looking away, so they see it coming.")]
        public bool waitForPlayerLook = false;
        [Range(10f, 90f)] public float lookAngle = 55f;
        [Tooltip("Longest it will wait for the player to look. After this it bites anyway.")]
        public float maxLookWait = 1.5f;

        [Header("Hit and retreat (alpha)")]
        [Tooltip("After each bite, back away slowly while facing the player, wind up, then charge again.")]
        public bool retreatAfterAttack = false;
        public float retreatDistance = 9f;
        public float retreatSpeed = 1.6f;
        [Tooltip("Seconds it stands and glares before charging again.")]
        public Vector2 windUpTime = new Vector2(1.0f, 2.0f);

        [Header("Sidestepping (circle the player instead of standing still)")]
        [Tooltip("Between attacks, and sometimes instead of a pause, step sideways around the player while facing it.")]
        public bool sidestep = true;
        public float strafeSpeed = 1.8f;
        [Tooltip("Seconds each sideways run lasts before it may change direction or stop.")]
        public Vector2 strafeLegDuration = new Vector2(1.0f, 2.6f);
        [Tooltip("Chance that a strafe leg is a short stand-and-watch instead of moving.")]
        [Range(0f, 1f)] public float strafeHoldChance = 0.2f;
        [Tooltip("Chance that a far-away pause becomes a sideways shuffle instead of standing still.")]
        [Range(0f, 1f)] public float sidestepInsteadOfPause = 0.65f;
        [Tooltip("While circling between attacks, keep this fraction of attackRange from the player.")]
        [Range(0.3f, 1f)] public float strafeRangeFraction = 0.85f;

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
        public string dieIndexParam = "DieIndex";    // int: which death variant
        public int deathVariants = 3;
        public string strafeParam = "Strafe";        // float: -1 sideways left .. 0 forward .. +1 sideways right

        protected NavMeshAgent agent;
        protected bool isDead;
        protected bool isAttacking;
        protected float nextAttackTime;

        float damageTaken;
        bool retreating;
        float retreatUntil;
        float lookWaitStart = -1f;
        float slowUntil;
        float nextRepath;
        float pausedUntil;
        float personalSpeed = 1f;
        float flank;
        float noiseSeed;

        public bool IsDead => isDead;

        // Read-only hooks for visuals (ProceduralDinoAnim). They do not change behaviour.
        public bool IsAttacking => isAttacking;
        public event System.Action AttackStarted;        // an attack has just begun
        public event System.Action<HitZone> WasHit;      // a hit that did not kill
        public event System.Action Died;                 // the dinosaur has just died
        // Sound hooks (DinoAudio). They do not change behaviour.
        public event System.Action Paused;                         // stopped to sniff / look around
        public event System.Action ChargeStarted;                  // entered charge distance
        public event System.Action<HitZone, Vector3> Hit;          // any arrow hit, including the killing one, with the hit point

        bool charging;

        // sidestep state
        bool strafing;
        float strafeDir = 1f;
        float strafeLegUntil;
        bool strafeHolding;
        float strafeAnim;

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

            KeepOutOfPlayersFace(player, dist);

            if (retreating)
            {
                UpdateRetreat(player, dist);
                return;
            }

            if (dist <= attackRange && CanAttack(player))
            {
                bool ready = Time.time >= nextAttackTime;
                if (!ready && sidestep)
                {
                    // Waiting for the next attack: circle sideways instead of standing still.
                    Strafe(player, dist, Mathf.Clamp(dist, attackRange * 0.5f, attackRange * strafeRangeFraction));
                    return;
                }
                StopStrafe();
                agent.isStopped = true;
                agent.velocity = Vector3.zero;   // plant its feet instead of sliding through the player
                FaceTowards(player.position);
                if (ready && PlayerIsWatching(player))
                {
                    lookWaitStart = -1f;
                    nextAttackTime = Time.time + attackCooldown * Random.Range(0.85f, 1.2f);
                    AttackStarted?.Invoke();
                    StartCoroutine(AttackRoutine(player));
                }
                return;
            }

            // Occasional pause while still far away: stop, then carry on.
            // Long-range attackers (Gorgosaurus spit) start their direct approach at their attack range, not before it.
            float directFrom = Mathf.Max(directApproachDistance, attackRange);
            float wander01 = Mathf.InverseLerp(directFrom, directFrom + wanderFadeDistance, dist);
            if (Time.time < pausedUntil)
            {
                if (strafing) { Strafe(player, dist, dist); return; }   // sideways shuffle "pause"
                agent.isStopped = true;
                FaceTowards(player.position, 0.3f);
                return;
            }
            if (wander01 >= 1f && Random.value < pauseChancePerSecond * Time.deltaTime)
            {
                pausedUntil = Time.time + Random.Range(pauseDuration.x, pauseDuration.y);
                if (sidestep && Random.value < sidestepInsteadOfPause) BeginStrafe(false);
                else StopStrafe();
                Paused?.Invoke();
                return;
            }

            StopStrafe();
            agent.isStopped = false;
            if (!charging && dist <= chargeDistance) { charging = true; ChargeStarted?.Invoke(); }
            else if (charging && dist > chargeDistance * 1.5f) charging = false;
            agent.speed = CurrentSpeed(dist);
            agent.autoBraking = wander01 <= 0f;   // brake on the final run-in so it stops at biting range
            // While circling in, targets are waypoints a few metres ahead: never "arrive" early at one.
            // (A stopping distance bigger than lookAhead froze long-range dinosaurs in place right after spawning.)
            agent.stoppingDistance = wander01 > 0f ? 0.5f : (dist <= attackRange ? 1f : attackRange * 0.8f);

            if (Time.time >= nextRepath)
            {
                nextRepath = Time.time + Random.Range(repathInterval.x, repathInterval.y);
                agent.SetDestination(PickMoveTarget(player.position, dist, wander01));
            }
        }

        // ---------- sidestepping ----------

        void BeginStrafe(bool allowHold)
        {
            if (!strafing) strafeDir = Random.value < 0.5f ? -1f : 1f;
            else if (Random.value < 0.45f) strafeDir = -strafeDir;
            strafing = true;
            strafeHolding = allowHold && Random.value < strafeHoldChance;
            strafeLegUntil = Time.time + Random.Range(strafeLegDuration.x, strafeLegDuration.y) * (strafeHolding ? 0.5f : 1f);
            agent.updateRotation = false;   // keep facing the player while moving sideways
        }

        void StopStrafe()
        {
            if (!strafing) return;
            strafing = false;
            agent.updateRotation = true;
        }

        // Moves around the player on a circle of the given radius, facing it, in strafeDir (+1 = to its right).
        void Strafe(Transform player, float dist, float radius)
        {
            if (!strafing || Time.time >= strafeLegUntil) BeginStrafe(true);
            FaceTowards(player.position);

            if (strafeHolding)
            {
                agent.isStopped = true;
                return;
            }

            Vector3 fromPlayer = transform.position - player.position;
            fromPlayer.y = 0f;
            if (fromPlayer.sqrMagnitude < 0.01f) fromPlayer = -player.forward;
            float stepAngle = strafeSpeed * 0.6f / Mathf.Max(1f, radius) * Mathf.Rad2Deg;
            // Our right, seen from the dinosaur facing the player, is a clockwise turn around the player.
            Vector3 next = Quaternion.Euler(0f, -strafeDir * stepAngle, 0f) * fromPlayer.normalized * radius;
            Vector3 target = player.position + next;
            target.y = transform.position.y;

            agent.isStopped = false;
            agent.speed = strafeSpeed * (Time.time < slowUntil ? legSlowMultiplier : 1f);
            agent.autoBraking = false;
            agent.stoppingDistance = 0.1f;
            if (Time.time >= nextRepath)
            {
                nextRepath = Time.time + 0.25f;
                if (NavMesh.SamplePosition(target, out NavMeshHit hit, 2f, NavMesh.AllAreas)) agent.SetDestination(hit.position);
                else { strafeDir = -strafeDir; strafeLegUntil = Time.time + 0.8f; }   // blocked: go the other way
            }
        }

        // True when the bite may start: either we don't care, the player is looking this way, or we've waited long enough.
        bool PlayerIsWatching(Transform player)
        {
            if (!waitForPlayerLook) return true;
            Vector3 target = (mouth != null ? mouth.position : transform.position + Vector3.up * 1.5f) - player.position;
            if (Vector3.Angle(player.forward, target) <= lookAngle) return true;
            if (lookWaitStart < 0f) lookWaitStart = Time.time;
            return Time.time - lookWaitStart >= maxLookWait;
        }

        // Stops the head from passing through the player: if the mouth gets closer than faceGap, slide the body back.
        void KeepOutOfPlayersFace(Transform player, float dist)
        {
            if (mouth == null) return;
            Vector3 away = transform.position - player.position;
            away.y = 0f;
            if (away.sqrMagnitude < 0.0001f) away = -player.forward;
            away.Normalize();
            float mouthDist = FlatDistance(mouth.position, player.position);
            // Only counts when the mouth is on the player's side of the body (it normally is).
            float overlap = faceGap - mouthDist;
            if (FlatDistance(mouth.position, transform.position) > dist) overlap = faceGap + mouthDist;
            if (overlap > 0f) agent.Move(away * Mathf.Min(overlap, 4f * Time.deltaTime));
        }

        void BeginRetreat()
        {
            retreating = true;
            retreatUntil = Time.time + retreatDistance / Mathf.Max(0.1f, retreatSpeed) + 2f;
            agent.updateRotation = false;   // walk backwards, eyes on the player
            pausedUntil = 0f;
        }

        void UpdateRetreat(Transform player, float dist)
        {
            FaceTowards(player.position);
            if (dist >= retreatDistance || Time.time >= retreatUntil)
            {
                retreating = false;
                agent.updateRotation = true;
                agent.isStopped = true;
                pausedUntil = Time.time + Random.Range(windUpTime.x, windUpTime.y);   // wind up, then the normal charge takes over
                return;
            }
            Vector3 away = transform.position - player.position;
            away.y = 0f;
            Vector3 target = player.position + away.normalized * (retreatDistance + 1f);
            target.y = transform.position.y;
            agent.isStopped = false;
            agent.speed = retreatSpeed;
            agent.autoBraking = true;
            agent.stoppingDistance = 0f;
            if (Time.time >= nextRepath)
            {
                nextRepath = Time.time + 0.4f;
                if (NavMesh.SamplePosition(target, out NavMeshHit hit, 4f, NavMesh.AllAreas)) agent.SetDestination(hit.position);
                else retreatUntil = 0f;   // nowhere to back into: skip straight to the wind-up
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
            Vector3 vel = agent.enabled ? agent.velocity : Vector3.zero;
            vel.y = 0f;
            float v = vel.magnitude;
            SetAnimFloat(speedParam, v);

            // -1..+1 when moving sideways (relative to where the dinosaur faces), 0 when moving forward or standing.
            float side = Vector3.Dot(vel, transform.right);
            float fwd = Vector3.Dot(vel, transform.forward);
            float strafeTarget = (v > 0.3f && Mathf.Abs(side) > Mathf.Abs(fwd)) ? Mathf.Sign(side) : 0f;
            strafeAnim = Mathf.MoveTowards(strafeAnim, strafeTarget, Time.deltaTime * 4f);
            SetAnimFloat(strafeParam, strafeAnim);

            if (animator != null && syncAnimationToSpeed && !HasParam(speedParam))
            {
                float target = Mathf.Clamp(v / Mathf.Max(0.1f, moveSpeed), animationSpeedRange.x, animationSpeedRange.y);
                // Sidestep clips are authored for a normal pace: play them near full speed.
                target = Mathf.Lerp(target, Mathf.Clamp(v / Mathf.Max(0.1f, strafeSpeed), 0.6f, 1.3f), Mathf.Abs(strafeAnim));
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

            yield return new WaitForSeconds(biteDelay);

            if (!isDead && player != null &&
                FlatDistance(transform.position, player.position) <= attackRange * biteReachMultiplier)
            {
                GameEvents.RaisePlayerHit(damage, transform.position);
                bitesLanded++;
                if (fragileAfterBites > 0 && bitesLanded == fragileAfterBites) BecameFragile?.Invoke(this);
            }

            yield return new WaitForSeconds(recoverTime);
            isAttacking = false;
            if (retreatAfterAttack) BeginRetreat();
        }

        // ---------- IDamageable (called by Zahid's arrow) ----------

        public void TakeHit(HitZone zone, float drawStrength01, Vector3 hitPoint)
        {
            if (isDead) return;

            pausedUntil = 0f;   // getting shot snaps it out of a pause
            Hit?.Invoke(zone, hitPoint);
            if (fragileAfterBites > 0)
            {
                if (IsFragile) { Die(); return; }
                WasHit?.Invoke(zone);   // flinches, but can't be hurt until it has bitten enough
                return;
            }
            switch (zone)
            {
                case HitZone.Head:
                    damageTaken += headShotWorth > 0f ? headShotWorth : (float)bodyHitsToKill / Mathf.Max(1, headHitsToKill);
                    if (damageTaken >= bodyHitsToKill - 0.01f) { Die(); return; }
                    break;
                case HitZone.Body:
                    damageTaken += 1f;
                    if (damageTaken >= bodyHitsToKill - 0.01f) { Die(); return; }
                    break;
                case HitZone.Legs:
                    slowUntil = Time.time + legSlowDuration;
                    break;
            }
            SetAnimTrigger(hitTrigger);
            WasHit?.Invoke(zone);
        }

        protected virtual void Die()
        {
            if (isDead) return;
            isDead = true;
            StopAllCoroutines();
            isAttacking = false;
            retreating = false;

            if (agent.isOnNavMesh) agent.isStopped = true;
            agent.enabled = false;
            foreach (var c in GetComponentsInChildren<Collider>()) c.enabled = false;

            StopStrafe();
            Died?.Invoke();
            if (HasParam(dieTrigger))
            {
                // A real death animation: pick one of the variants and let it play at normal speed.
                if (HasParam(dieIndexParam)) animator.SetInteger(dieIndexParam, Random.Range(0, Mathf.Max(1, deathVariants)));
                animator.speed = 1f;
                SetAnimTrigger(dieTrigger);
            }
            else if (GetComponent<ProceduralDinoAnim>() == null)   // stand-in when nothing else animates the death (sound listeners on Died don't count)
            {
                if (animator != null) animator.speed = 0f;   // freeze the pose
                StartCoroutine(FallOver());
            }

            GameEvents.RaiseDinoDied(gameObject);
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
