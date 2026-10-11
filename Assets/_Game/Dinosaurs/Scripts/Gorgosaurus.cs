using System.Collections;
using UnityEngine;

namespace BowGame
{
    // Slower, tougher dinosaur. Walks in with the shared natural movement, stops at range
    // and spits a visible glob of acid at the player. Uses the shared recoverTime.
    public class Gorgosaurus : Dinosaur
    {
        [Header("Gorgosaurus acid spit")]
        public SpitProjectile spitPrefab;
        [Tooltip("Where the spit leaves the mouth. Falls back to this object plus spitHeight.")]
        public Transform spitOrigin;
        public float spitHeight = 2f;
        [Tooltip("Seconds from the start of the attack to releasing the spit (match the attack animation).")]
        public float spitDelay = 0.5f;
        [Tooltip("Only spit if nothing blocks the view to the player.")]
        public bool needLineOfSight = true;
        public LayerMask sightBlockers = ~0;

        void Reset()
        {
            bodyHitsToKill = 5;
            moveSpeed = 3f;
            turnSpeed = 180f;
            attackRange = 14f;
            attackCooldown = 3.5f;
            damage = 8f;
            recoverTime = 0.8f;
            weaveAmplitude = 4f;
            weaveFrequency = 0.08f;
            pauseChancePerSecond = 0.08f;
        }

        Vector3 MouthPosition => spitOrigin != null ? spitOrigin.position : transform.position + Vector3.up * spitHeight;

        protected override bool CanAttack(Transform player)
        {
            if (spitPrefab == null) return false;
            if (!needLineOfSight) return true;

            int mask = sightBlockers;
            foreach (var n in new[] { "Dinosaur", "Player", "EnemyProjectile", "Arrow" })
            {
                int l = LayerMask.NameToLayer(n);
                if (l >= 0) mask &= ~(1 << l);
            }
            // Blocked only if something that isn't part of the player (terrain, trees, rocks) is in the way.
            if (Physics.Linecast(MouthPosition, player.position, out RaycastHit hit, mask, QueryTriggerInteraction.Ignore))
                return hit.transform.root == player.root;
            return true;
        }

        protected override IEnumerator AttackRoutine(Transform player)
        {
            isAttacking = true;
            SetAnimTrigger(attackTrigger);

            yield return new WaitForSeconds(spitDelay);

            if (!isDead && player != null && spitPrefab != null)
            {
                var spit = Instantiate(spitPrefab, MouthPosition, Quaternion.identity);
                spit.Launch(player, damage, transform);
            }

            yield return new WaitForSeconds(recoverTime);
            isAttacking = false;
        }
    }
}
