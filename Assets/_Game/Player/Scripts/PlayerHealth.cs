using System;
using UnityEngine;

namespace BowGame
{
    // Player health. Dinosaurs hurt the player by calling GameEvents.RaisePlayerHit(damage, attackerPosition).
    public class PlayerHealth : MonoBehaviour
    {
        public static PlayerHealth Instance;
        public static event Action<HitSide, float> Damaged;   // side, damage (for the HUD flash)

        public Transform head;                 // the VR camera
        public float maxHealth = 100f;
        public float fullBuzzDamage = 30f;     // damage that gives the strongest vest buzz

        public float Health { get; private set; }
        public float Health01 => maxHealth > 0 ? Health / maxHealth : 0f;
        public bool IsDead { get; private set; }

        void OnEnable()
        {
            Instance = this;
            Health = maxHealth; IsDead = false;
            if (head != null) GameRefs.PlayerHead = head;
            GameEvents.PlayerHit += OnHit;
            GameEvents.StateChanged += OnState;
        }

        void OnDisable()
        {
            GameEvents.PlayerHit -= OnHit;
            GameEvents.StateChanged -= OnState;
            if (Instance == this) Instance = null;
            if (GameRefs.PlayerHead == head) GameRefs.PlayerHead = null;
        }

        // Refill health (used by test scenes and when a round restarts).
        public void ResetHealth() { Health = maxHealth; IsDead = false; }

        void OnState(GameState s)
        {
            if (s == GameState.Calibrate || s == GameState.Countdown) { Health = maxHealth; IsDead = false; }
        }

        void OnHit(float damage, Vector3 from)
        {
            if (IsDead) return;
            HitSide side = SideOf(from);
            Health = Mathf.Max(0f, Health - damage);
            Haptics.Hit(side, damage / Mathf.Max(1f, fullBuzzDamage));
            Damaged?.Invoke(side, damage);
            if (Health <= 0f) { IsDead = true; GameEvents.RaisePlayerDied(); }
        }

        HitSide SideOf(Vector3 from)
        {
            if (head == null) return HitSide.Front;
            Vector3 local = head.InverseTransformPoint(from);
            local.y = 0f;
            float angle = Mathf.Atan2(local.x, local.z) * Mathf.Rad2Deg;   // 0 = straight ahead, + = right
            if (Mathf.Abs(angle) <= 45f) return HitSide.Front;
            return angle > 0f ? HitSide.Right : HitSide.Left;              // hits from behind go to the nearer side
        }
    }
}
