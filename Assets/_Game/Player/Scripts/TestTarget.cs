using UnityEngine;

namespace BowGame
{
    // Test-scene target: flashes and logs when an arrow hits it. Not used in the real game.
    public class TestTarget : MonoBehaviour, IDamageable
    {
        Renderer rend;
        Color baseColor = Color.white;
        float flash;

        void Awake()
        {
            rend = GetComponent<Renderer>();
            if (rend != null) baseColor = rend.material.color;
        }

        public void TakeHit(HitZone zone, float drawStrength01, Vector3 hitPoint)
        {
            Debug.Log("[TestTarget] " + name + " hit: " + zone + ", draw " + drawStrength01.ToString("0.00"));
            flash = 1f;
        }

        void Update()
        {
            if (rend == null || flash <= 0f) return;
            flash = Mathf.Max(0f, flash - Time.deltaTime * 2f);
            rend.material.color = Color.Lerp(baseColor, Color.red, flash);
        }
    }
}
