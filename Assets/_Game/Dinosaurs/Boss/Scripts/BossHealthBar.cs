using UnityEngine;

namespace BowGame
{
    // Simple world-space health bar that floats above the boss and turns to face the player's headset.
    // Built from two quads at runtime, so it works in VR without a Canvas. Turns red when the boss enrages
    // and disappears when it dies.
    public class BossHealthBar : MonoBehaviour
    {
        public BossEnemy boss;
        [Tooltip("Bone the bar floats above (the head). Empty = the boss's root plus height.")]
        public Transform anchor;
        public float heightAbove = 0.55f;
        public Vector2 size = new Vector2(1.2f, 0.09f);
        public Color fillColour = new Color(0.85f, 0.75f, 0.2f);
        public Color rageColour = new Color(0.9f, 0.15f, 0.1f);
        public Color backColour = new Color(0f, 0f, 0f, 0.6f);
        [Tooltip("How quickly the bar slides down to the new value.")]
        public float smoothing = 6f;

        Transform bar, fill;
        Material fillMat;
        float shown = 1f;

        void Awake()
        {
            if (boss == null) boss = GetComponentInParent<BossEnemy>();
            if (anchor == null && boss != null) anchor = boss.mouth;

            bar = new GameObject("BossHealthBar").transform;
            bar.SetParent(transform, false);
            var back = Quad("Back", backColour, 0f);
            back.localScale = new Vector3(size.x + 0.04f, size.y + 0.04f, 1f);
            fill = Quad("Fill", fillColour, -0.002f);
            fillMat = fill.GetComponent<MeshRenderer>().material;
        }

        Transform Quad(string name, Color c, float z)
        {
            var q = GameObject.CreatePrimitive(PrimitiveType.Quad);
            q.name = name;
            Destroy(q.GetComponent<Collider>());
            q.transform.SetParent(bar, false);
            q.transform.localPosition = new Vector3(0f, 0f, z);
            var r = q.GetComponent<MeshRenderer>();
            r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            r.receiveShadows = false;
            var shader = Shader.Find("Universal Render Pipeline/Unlit");
            var m = new Material(shader != null ? shader : Shader.Find("Unlit/Color"));
            if (c.a < 1f && m.HasProperty("_Surface"))
            {
                m.SetFloat("_Surface", 1f);   // transparent
                m.SetFloat("_Blend", 0f);
                m.SetOverrideTag("RenderType", "Transparent");
                m.SetInt("_SrcBlend", (int)UnityEngine.Rendering.BlendMode.SrcAlpha);
                m.SetInt("_DstBlend", (int)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha);
                m.SetInt("_ZWrite", 0);
                m.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
                m.renderQueue = (int)UnityEngine.Rendering.RenderQueue.Transparent;
            }
            if (m.HasProperty("_BaseColor")) m.SetColor("_BaseColor", c); else m.color = c;
            r.material = m;
            return q.transform;
        }

        void LateUpdate()
        {
            if (boss == null || boss.IsDead) { if (bar.gameObject.activeSelf) bar.gameObject.SetActive(false); return; }

            Vector3 basePos = anchor != null ? anchor.position : boss.transform.position + Vector3.up * 2.5f;
            bar.position = basePos + Vector3.up * heightAbove;
            Transform head = GameRefs.PlayerHead != null ? GameRefs.PlayerHead : (Camera.main != null ? Camera.main.transform : null);
            if (head != null)
            {
                Vector3 look = bar.position - head.position;
                look.y = 0f;
                if (look.sqrMagnitude > 0.001f) bar.rotation = Quaternion.LookRotation(look);
            }

            shown = Mathf.MoveTowards(shown, boss.Health01, Time.deltaTime * Mathf.Max(0.05f, Mathf.Abs(shown - boss.Health01) * smoothing));
            fill.localScale = new Vector3(size.x * shown, size.y, 1f);
            fill.localPosition = new Vector3(-size.x * (1f - shown) * 0.5f, 0f, -0.002f);
            Color c = boss.Raging ? rageColour : fillColour;
            if (fillMat.HasProperty("_BaseColor")) fillMat.SetColor("_BaseColor", c); else fillMat.color = c;
        }

        void OnDestroy()
        {
            if (fillMat != null) Destroy(fillMat);
        }
    }
}
