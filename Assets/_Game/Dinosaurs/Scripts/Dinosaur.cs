using UnityEngine;
using UnityEngine.AI;

namespace BowGame
{
    // Shared dinosaur brain: chase the player, attack in range, take arrow hits, die.
    // Maip and Gorgosaurus inherit from this and only supply their own attack.
    // Works with or without an Animator, so it can be tested on a capsule stand-in.
    [RequireComponent(typeof(NavMeshAgent))]
    public abstract class Dinosaur : MonoBehaviour, IDamageable
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
        [Tooltip("How often the path to the player is recalculated, in seconds.")]
        public float repathInterval = 0.25f;

        [Header("Attack")]
        [Tooltip("Starts an attack when the player is this close (metres, measured flat on the ground).")]
        public float attackRange = 2.5f;
        public float attackCooldown = 1.5f;
        public float damage = 10f;

        [Header("Death")]
        [Tooltip("Seconds the body stays before it is removed.")]
        public float corpseTime = 4f;

        [Header("Animation (optional)")]
        public Animator animator;
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

        public bool IsDead => isDead;

        // Read-only hooks for visuals (ProceduralDinoAnim). They do not change behaviour.
        public bool IsAttacking => isAttacking;
        public event System.Action AttackStarted;        // an attack has just begun
        public event System.Action<HitZone> WasHit;      // a hit that did not kill
        public event System.Action Died;                 // the dinosaur has just died

        protected virtual void Awake()
        {
            agent = GetComponent<NavMeshAgent>();
            if (animator == null) animator = GetComponentInChildren<Animator>();
        }

        protected virtual void Start()
        {
            agent.speed = moveSpeed;
            agent.stoppingDistance = attackRange * 0.8f;
        }

        protected virtual void Update()
        {
            if (isDead) return;

            agent.speed = moveSpeed * (Time.time < slowUntil ? legSlowMultiplier : 1f);
            SetAnimFloat(speedParam, agent.velocity.magnitude);

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
            }
            else if (dist <= attackRange && CanAttack(player))
            {
                agent.isStopped = true;
                FaceTowards(player.position);
                if (Time.time >= nextAttackTime)
                {
                    nextAttackTime = Time.time + attackCooldown;
                    AttackStarted?.Invoke();
                    StartCoroutine(AttackRoutine(player));
                }
            }
            else
            {
                agent.isStopped = false;
                // In range but can't attack (e.g. view blocked): walk closer instead of stopping.
                agent.stoppingDistance = dist <= attackRange ? 1f : attackRange * 0.8f;
                if (Time.time >= nextRepath)
                {
                    nextRepath = Time.time + repathInterval;
                    agent.SetDestination(player.position);
                }
            }
        }

        // Override for extra conditions (for example line of sight).
        protected virtual bool CanAttack(Transform player) => true;

        // Each dinosaur type plays its own attack. Set isAttacking false when finished.
        protected abstract System.Collections.IEnumerator AttackRoutine(Transform player);

        // ---------- IDamageable (called by Zahid's arrow) ----------

        public void TakeHit(HitZone zone, float drawStrength01, Vector3 hitPoint)
        {
            if (isDead) return;

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
            WasHit?.Invoke(zone);
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

            Died?.Invoke();
            if (HasParam(dieTrigger)) SetAnimTrigger(dieTrigger);
            else if (Died == null) StartCoroutine(FallOver());   // stand-in when nothing else animates the death

            GameEvents.RaiseDinoDied(gameObject);
            Destroy(gameObject, corpseTime);
        }

        System.Collections.IEnumerator FallOver()
        {
            Quaternion from = transform.rotation;
            Quaternion to = from * Quaternion.Euler(0f, 0f, 90f);
            for (float t = 0f; t < 1f; t += Time.deltaTime * 2f)
            {
                transform.rotation = Quaternion.Slerp(from, to, t);
                yield return null;
            }
            transform.rotation = to;
        }

        // ---------- helpers ----------

        protected void FaceTowards(Vector3 target)
        {
            Vector3 dir = target - transform.position;
            dir.y = 0f;
            if (dir.sqrMagnitude < 0.0001f) return;
            transform.rotation = Quaternion.RotateTowards(transform.rotation, Quaternion.LookRotation(dir), turnSpeed * Time.deltaTime);
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
