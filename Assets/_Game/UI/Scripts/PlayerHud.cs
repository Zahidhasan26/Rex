using TMPro;
using UnityEngine;

namespace BowGame
{
    // Small HUD mounted on the bow (health, draw, time) plus a centre message and a red hit flash.
    public class PlayerHud : MonoBehaviour
    {
        public Transform healthFill;
        public Transform drawFill;
        public Renderer drawRenderer;
        public TMP_Text timerText;
        public TMP_Text messageText;
        public Renderer flashRenderer;
        public Transform flashQuad;
        public float barWidth = 0.12f;

        public static PlayerHud Instance;

        float flash;
        Color flashColor = new Color(1f, 0f, 0f, 0f);
        float messageUntil = -1f;

        void OnEnable()
        {
            Instance = this;
            PlayerHealth.Damaged += OnDamaged;
            GameEvents.StateChanged += OnState;
            GameEvents.PlayerDied += OnDied;
            if (messageText != null) messageText.text = "";
            SetFlash(0f);
        }

        void OnDisable()
        {
            if (Instance == this) Instance = null;
            PlayerHealth.Damaged -= OnDamaged;
            GameEvents.StateChanged -= OnState;
            GameEvents.PlayerDied -= OnDied;
        }

        void OnDamaged(HitSide side, float damage)
        {
            flash = Mathf.Clamp01(0.35f + damage / 60f);
            if (flashQuad != null)
            {
                var p = flashQuad.localPosition;
                p.x = side == HitSide.Left ? -0.12f : side == HitSide.Right ? 0.12f : 0f;
                flashQuad.localPosition = p;
            }
        }

        void OnState(GameState s)
        {
            switch (s)
            {
                case GameState.Calibrate:      Show("Relax your arm, then flex hard", 60f); break;
                case GameState.Countdown:      Show("Get ready", 60f); break;
                case GameState.Survive:        Show("Survive!", 2f); break;
                case GameState.ActOneComplete: Show("You survived", 60f); break;
                case GameState.Dead:           Show("You died", 60f); break;
            }
        }

        void OnDied() { Show("You died", 3f); }

        public void Show(string text, float seconds)
        {
            if (messageText == null) return;
            messageText.text = text;
            messageUntil = Time.time + seconds;
        }

        void Update()
        {
            float hp = PlayerHealth.Instance != null ? PlayerHealth.Instance.Health01 : 1f;
            SetBar(healthFill, hp);
            SetBar(drawFill, Bow.CurrentDraw);
            if (drawRenderer != null) drawRenderer.material.color = Color.Lerp(new Color(1f, 0.9f, 0.3f), new Color(1f, 0.3f, 0.1f), Bow.CurrentDraw);

            if (timerText != null)
            {
                bool timed = GameRefs.State == GameState.Countdown || GameRefs.State == GameState.Survive;
                float t = Mathf.Max(0f, GameRefs.StateTimeLeft);
                timerText.text = timed && t > 0f ? Mathf.FloorToInt(t / 60f) + ":" + Mathf.FloorToInt(t % 60f).ToString("00") : "";
            }

            if (messageText != null && messageUntil > 0f && Time.time > messageUntil) { messageText.text = ""; messageUntil = -1f; }

            if (flash > 0f) { flash = Mathf.Max(0f, flash - Time.deltaTime * 1.6f); SetFlash(flash * 0.6f); }
        }

        void SetBar(Transform fill, float v)
        {
            if (fill == null) return;
            v = Mathf.Clamp01(v);
            var s = fill.localScale; s.x = Mathf.Max(0.0001f, barWidth * v); fill.localScale = s;
            var p = fill.localPosition; p.x = -barWidth * 0.5f + barWidth * v * 0.5f; fill.localPosition = p;
        }

        void SetFlash(float alpha)
        {
            if (flashRenderer == null) return;
            flashColor.a = alpha;
            flashRenderer.material.color = flashColor;
            flashRenderer.enabled = alpha > 0.001f;
        }
    }
}
