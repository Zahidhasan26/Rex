using TMPro;
using UnityEngine;

namespace BowGame
{
    // A word that pops up at a hit, faces the player, rises and fades.
    public class FloatingText : MonoBehaviour
    {
        TextMeshPro text;
        Color color;
        float life = 0.9f, age, sizeScale = 1f;

        public static void Spawn(Vector3 position, string words, Color c, float size)
        {
            var go = new GameObject("FloatingText");
            go.transform.position = position;
            var ft = go.AddComponent<FloatingText>();
            ft.text = go.AddComponent<TextMeshPro>();
            ft.text.text = words;
            ft.text.fontSize = 4f;
            ft.text.alignment = TextAlignmentOptions.Center;
            ft.text.fontStyle = FontStyles.Bold;
            ft.text.rectTransform.sizeDelta = new Vector2(8f, 2f);
            ft.color = c; ft.sizeScale = size;
            ft.text.color = c;
            ft.Face();
        }

        void Face()
        {
            Transform head = GameRefs.PlayerHead != null ? GameRefs.PlayerHead : (Camera.main != null ? Camera.main.transform : null);
            if (head == null) return;
            Vector3 away = transform.position - head.position;
            if (away.sqrMagnitude > 0.0001f) transform.rotation = Quaternion.LookRotation(away);
            float dist = away.magnitude;
            transform.localScale = Vector3.one * Mathf.Clamp(dist * 0.045f, 0.12f, 1.6f) * sizeScale;
        }

        void Update()
        {
            age += Time.deltaTime;
            transform.position += Vector3.up * (0.7f * Time.deltaTime);
            Face();
            color.a = Mathf.Clamp01(1f - age / life);
            if (text != null) text.color = color;
            if (age >= life) Destroy(gameObject);
        }
    }
}
