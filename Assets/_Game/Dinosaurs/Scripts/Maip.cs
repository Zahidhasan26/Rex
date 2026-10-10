using System.Collections;
using UnityEngine;

namespace BowGame
{
    // Fast melee attacker. Runs at the player and bites.
    public class Maip : Dinosaur
    {
        [Header("Maip bite")]
        [Tooltip("Seconds from the start of the attack to the moment the bite lands (match the attack animation).")]
        public float biteDelay = 0.35f;
        [Tooltip("Seconds after the bite before the Maip can move again.")]
        public float recoverTime = 0.4f;
        [Tooltip("Player must still be within attackRange times this when the bite lands, or it misses.")]
        public float biteReachMultiplier = 1.2f;

        void Reset()
        {
            bodyHitsToKill = 3;
            moveSpeed = 6f;
            attackRange = 2.2f;
            attackCooldown = 1.2f;
            damage = 10f;
        }

        protected override IEnumerator AttackRoutine(Transform player)
        {
            isAttacking = true;
            SetAnimTrigger(attackTrigger);

            yield return new WaitForSeconds(biteDelay);

            if (!isDead && player != null &&
                FlatDistance(transform.position, player.position) <= attackRange * biteReachMultiplier)
            {
                GameEvents.RaisePlayerHit(damage, transform.position);
            }

            yield return new WaitForSeconds(recoverTime);
            isAttacking = false;
        }
    }
}
