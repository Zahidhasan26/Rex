using System;
using System.Collections;
using UnityEngine;

namespace BowGame
{
    // Final-act boss. Reuses the dinosaur brain (natural approach, sidestepping, hit zones, deaths)
    // and swaps the bite for two attacks:
    //   Throw  - winds up and hurls a dark orb (SpitProjectile) from its right hand. Used at range.
    //   Charge - plants its feet, roars, then sprints straight at the player and finishes with an
    //            overhead two-handed smash. Can be sidestepped: it only steers a little while running.
    // Close up it smashes without the run. Below rageAtHealth it attacks more often and charges more.
    public class BossEnemy : Dinosaur
    {
        [Header("Boss: thrown orb")]
        public SpitProjectile projectilePrefab;
        [Tooltip("Hand bone the orb leaves from.")]
        public Transform throwHand;
        [Tooltip("Seconds into the throw animation when the orb is released.")]
        public float throwRelease = 0.62f;
        public float throwDamage = 10f;
        [Tooltip("Orb visible in the hand during the wind-up (optional).")]
        public GameObject heldOrb;

        [Header("Boss: charge and smash")]
        [Range(0f, 1f)] public float chargeChance = 0.4f;
        [Tooltip("Only charges from at least this far away; closer it just smashes.")]
        public float minChargeDistance = 5f;
        public float chargeWindup = 0.7f;
        public float chargeRunSpeed = 7.5f;
        [Tooltip("How fast it can steer while running (degrees per second). Low = easy to dodge.")]
        public float chargeTurnRate = 50f;
        public float maxChargeTime = 2.6f;
        [Tooltip("Starts the smash when the player is this far ahead (or already beside it).")]
        public float smashStartDistance = 1.6f;
        [Tooltip("Inside this distance the run stops steering and goes straight, so a late sidestep makes it miss.")]
        public float commitDistance = 6f;
        [Tooltip("Seconds into the smash animation when it lands.")]
        public float smashHitTime = 0.4f;
        [Tooltip("The smash hurts if the player is within this distance when it lands.")]
        public float smashReach = 2.4f;
        public float smashDamage = 25f;
        [Tooltip("The smash only hits in front of the boss, within this many degrees either side. A late sidestep dodges it.")]
        [Range(10f, 180f)] public float smashArc = 30f;
        [Tooltip("How fast it can still turn during a standing smash's wind-up (degrees per second).")]
        public float smashTurnSpeed = 45f;
        public float smashRecover = 0.8f;
        [Tooltip("Player this close: smash without charging.")]
        public float meleeRange = 2.4f;

        [Header("Boss: rage")]
        [Range(0f, 1f)] public float rageAtHealth = 0.5f;
        public float rageCooldownMultiplier = 0.65f;
        public float rageChanceBonus = 0.2f;

        [Header("Boss: animator names")]
        public string throwTrigger = "Attack";
        public string chargeTrigger = "ChargeWindup";
        public string chargingBool = "Charging";
        public string smashTrigger = "Swing";

        public static event Action<BossEnemy> Defeated;
        public event Action ThrowStarted, ChargeAnnounced, SmashStarted, Enraged;

        public bool Raging { get; private set; }
        public float Health01 => 1f - Mathf.Clamp01(damage01);

        float damage01;

        void Reset()
        {
            bodyHitsToKill = 24;
            headHitsToKill = 6;
            moveSpeed = 1.4f;
            turnSpeed = 140f;
            attackRange = 16f;
            attackCooldown = 3f;
            recoverTime = 0.7f;
            chargeDistance = 0f;
            directApproachDistance = 16f;
            weaveAmplitude = 2f;
            pauseChancePerSecond = 0.03f;
            strafeSpeed = 1.3f;
            strafeRangeFraction = 0.7f;
            corpseTime = 10f;
            deathVariants = 3;
            syncAnimationToSpeed = false;
        }

        protected override void Awake()
        {
            base.Awake();
            Hit += OnHit;
            // Don't queue a flinch while throwing or smashing; it would play late, after the attack.
            WasHit += z => { if (isAttacking && animator != null && HasParam(hitTrigger)) animator.ResetTrigger(hitTrigger); };
            if (heldOrb != null) heldOrb.SetActive(false);
        }

        protected override void Update()
        {
            base.Update();
            // Backing away while facing the player: drive the reversed walk in the blend tree (negative Speed).
            if (isDead || animator == null || !agent.enabled) return;
            Vector3 v = agent.velocity; v.y = 0f;
            float fwd = Vector3.Dot(v, transform.forward), side = Vector3.Dot(v, transform.right);
            if (fwd < -0.3f && Mathf.Abs(fwd) > Mathf.Abs(side) && HasParam(speedParam)) animator.SetFloat(speedParam, fwd);
        }

        void OnHit(HitZone zone, Vector3 point)
        {
            float k = zone == HitZone.Head ? 1f / Mathf.Max(1, headHitsToKill) : zone == HitZone.Body ? 1f / Mathf.Max(1, bodyHitsToKill) : 0f;
            damage01 += k;
            if (!Raging && Health01 <= rageAtHealth && !IsDead)
            {
                Raging = true;
                attackCooldown *= rageCooldownMultiplier;
                chargeChance = Mathf.Clamp01(chargeChance + rageChanceBonus);
                Enraged?.Invoke();
            }
        }

        protected override bool CanAttack(Transform player) => true;

        protected override IEnumerator AttackRoutine(Transform player)
        {
            float dist = FlatDistance(transform.position, player.position);
            if (dist <= meleeRange) return Smash(player, false);
            if (dist >= minChargeDistance && UnityEngine.Random.value < chargeChance) return Charge(player);
            if (projectilePrefab == null) return dist >= minChargeDistance ? Charge(player) : Smash(player, false);
            return Throw(player);
        }

        IEnumerator Throw(Transform player)
        {
            isAttacking = true;
            ThrowStarted?.Invoke();
            SetAnimTrigger(throwTrigger);
            if (heldOrb != null) heldOrb.SetActive(true);

            yield return new WaitForSeconds(throwRelease);
            if (heldOrb != null) heldOrb.SetActive(false);

            if (!isDead && player != null && projectilePrefab != null)
            {
                Vector3 from = throwHand != null ? throwHand.position : transform.position + Vector3.up * 2f;
                var orb = Instantiate(projectilePrefab, from, Quaternion.identity);
                orb.Launch(player, throwDamage, transform);
            }

            yield return new WaitForSeconds(recoverTime);
            EndAttack();
        }

        IEnumerator Charge(Transform player)
        {
            isAttacking = true;
            ChargeAnnounced?.Invoke();
            SetAnimTrigger(chargeTrigger);
            yield return new WaitForSeconds(chargeWindup);   // the base class keeps it facing the player meanwhile

            SetBool(chargingBool, true);
            Vector3 dir = Flat(player.position - transform.position).normalized;
            float t = 0f;
            bool committed = false;
            while (!isDead && player != null && t < maxChargeTime)
            {
                Vector3 to = Flat(player.position - transform.position);
                float ahead = Vector3.Dot(to, dir);
                // Swing once the player is level with the reach in front, or already beside or behind (it ran past).
                if (ahead <= smashStartDistance) break;
                // In the last few metres it stops steering: this is the moment to sidestep.
                if (!committed && to.magnitude <= commitDistance) committed = true;
                if (!committed) dir = Vector3.RotateTowards(dir, to.normalized, chargeTurnRate * Mathf.Deg2Rad * Time.deltaTime, 0f);
                agent.Move(dir * chargeRunSpeed * Time.deltaTime);
                transform.rotation = Quaternion.LookRotation(dir);
                t += Time.deltaTime;
                yield return null;
            }
            SetBool(chargingBool, false);
            if (isDead) yield break;
            yield return Smash(player, true);
        }

        IEnumerator Smash(Transform player, bool fromCharge)
        {
            isAttacking = true;
            SmashStarted?.Invoke();
            SetAnimTrigger(smashTrigger);
            // Commit to the swing: it barely turns during the wind-up (not at all after a charge), so a sidestep works.
            float savedTurn = turnSpeed;
            turnSpeed = fromCharge ? 0f : smashTurnSpeed;
            yield return new WaitForSeconds(smashHitTime);
            turnSpeed = savedTurn;

            if (!isDead && player != null && InSmashArc(player))
                GameEvents.RaisePlayerHit(smashDamage, transform.position);

            yield return new WaitForSeconds(smashRecover);
            EndAttack();
        }

        // The cooldown starts when an attack finishes, so a long charge isn't followed straight away by another attack.
        void EndAttack()
        {
            isAttacking = false;
            nextAttackTime = Mathf.Max(nextAttackTime, Time.time + attackCooldown * UnityEngine.Random.Range(0.6f, 0.9f));
        }

        bool InSmashArc(Transform player)
        {
            Vector3 to = Flat(player.position - transform.position);
            if (to.magnitude > smashReach) return false;
            return to.sqrMagnitude < 0.01f || Vector3.Angle(transform.forward, to) <= smashArc;
        }

        protected override void Die()
        {
            if (IsDead) return;
            SetBool(chargingBool, false);
            if (heldOrb != null) heldOrb.SetActive(false);
            base.Die();
            Defeated?.Invoke(this);
        }

        void SetBool(string name, bool value)
        {
            if (HasParam(name)) animator.SetBool(name, value);
        }

        static Vector3 Flat(Vector3 v) { v.y = 0f; return v; }
    }
}
