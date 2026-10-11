using Unity.XR.CoreUtils;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;

namespace BowGame
{
    // Dev tool for choosing the play area. In Play mode in the Main scene:
    //   left controller X (or keyboard N) = next spot, Y (or keyboard P) = previous spot,
//   click the left thumbstick (or keyboard M) = save where you are standing to Logs/MarkedSpots.txt.
    // Each spot is where that square's screenshot was taken (Logs/MapShotsOut), facing the view you liked.
    // Nothing to place in the scene; it adds itself. Set Enabled = false to switch it off everywhere.
    public class SpotTeleporter : MonoBehaviour
    {
        public static bool Enabled = true;

        struct Spot
        {
            public string name; public float x, z, yaw;
            public Spot(string n, float x, float z, float yaw) { name = n; this.x = x; this.z = z; this.yaw = yaw; }
        }

        // yaw: 0 = north, 90 = east, 180 = south, 270 = west
        static readonly Spot[] Spots =
        {
            new Spot("15  lake shore (east)",          0f,  250f,   0f),
            new Spot("14  lake shore (west)",       -200f,  250f,   0f),
            new Spot("10  flower shore, north view",   0f,  450f,   0f),
            new Spot("10  flower shore, east view",    0f,  450f,  90f),
            new Spot("02  small pond",              -560f,  630f,  20f),
            new Spot("17  dense tall trees",        -600f,   50f, 270f),
            new Spot("22  west view",               -600f, -150f, 270f),
            new Spot("ARENA IDEA A: edge of 22, facing the forest", -480f, -60f, 260f),
            new Spot("ARENA IDEA B: edge of 22/23, facing the forest", -470f, -120f, 270f),
            new Spot("23  hill to the west",        -400f, -150f, 270f),
        };

        XROrigin origin;
        InputAction next, prev, mark;
        int index = -1;
        TextMesh label;
        float hideAt;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        static void Init()
        {
            if (!Enabled || SceneManager.GetActiveScene().name != "Main") return;
            if (FindAnyObjectByType<SpotTeleporter>() != null) return;
            var o = FindAnyObjectByType<XROrigin>();
            if (o == null) return;
            var go = new GameObject("SpotTeleporter (dev)");
            go.AddComponent<SpotTeleporter>().origin = o;
        }

        void OnEnable()
        {
            next = new InputAction("NextSpot", InputActionType.Button);
            next.AddBinding("<XRController>{LeftHand}/primaryButton");
            next.AddBinding("<Keyboard>/n");
            prev = new InputAction("PrevSpot", InputActionType.Button);
            prev.AddBinding("<XRController>{LeftHand}/secondaryButton");
            prev.AddBinding("<Keyboard>/p");
            mark = new InputAction("MarkSpot", InputActionType.Button);
            mark.AddBinding("<XRController>{LeftHand}/primary2DAxisClick");
            mark.AddBinding("<Keyboard>/m");
            next.Enable();
            prev.Enable();
            mark.Enable();
        }

        void OnDisable()
        {
            next?.Disable(); next?.Dispose();
            prev?.Disable(); prev?.Dispose();
            mark?.Disable(); mark?.Dispose();
        }

        void Update()
        {
            if (origin == null) return;
            if (next.WasPressedThisFrame()) Go(index + 1);
            if (prev.WasPressedThisFrame()) Go(index - 1);
            if (mark.WasPressedThisFrame()) Mark();
            if (label != null && Time.time > hideAt) label.gameObject.SetActive(false);
        }

        void Go(int i)
        {
            index = (i % Spots.Length + Spots.Length) % Spots.Length;
            var s = Spots[index];
            var cam = origin.Camera.transform;

            var cc = origin.GetComponent<CharacterController>();
            if (cc != null) cc.enabled = false;

            // Turn so the head faces the spot's direction, then put the feet on the ground at the spot.
            float turn = Mathf.DeltaAngle(cam.eulerAngles.y, s.yaw);
            origin.RotateAroundCameraUsingOriginUp(turn);
            Vector3 feet = FindClearSpot(s.x, s.z);
            Vector3 headOffset = cam.position - origin.transform.position;
            headOffset.y = 0f;
            origin.transform.position = feet + Vector3.up * 0.05f - headOffset;

            if (cc != null) cc.enabled = true;
            Show((index + 1) + "/" + Spots.Length + "   " + s.name);
        }

        // The nearest spot (spiralling out up to ~15 m) that is not inside a tree, rock or bush and not steep.
        int marks;

        void Mark()
        {
            var cam = origin.Camera.transform;
            Vector3 p = cam.position;
            float yaw = cam.eulerAngles.y;
            marks++;
            string line = string.Format(System.Globalization.CultureInfo.InvariantCulture,
                "{0:yyyy-MM-dd HH:mm:ss}  mark {1}  x={2:F1} z={3:F1} ground={4:F1} facing={5:F0}  (last stop: {6})",
                System.DateTime.Now, marks, p.x, p.z, GroundAt(p.x, p.z), yaw, index >= 0 ? Spots[index].name : "none");
            try
            {
                string dir = System.IO.Path.Combine(Application.dataPath, "..", "Logs");
                System.IO.Directory.CreateDirectory(dir);
                System.IO.File.AppendAllText(System.IO.Path.Combine(dir, "MarkedSpots.txt"), line + System.Environment.NewLine);
            }
            catch (System.Exception e) { Debug.LogWarning("SpotTeleporter: could not save mark: " + e.Message); }
            Debug.Log("[SpotTeleporter] " + line);
            Show("Marked " + marks + "  (" + p.x.ToString("F0") + ", " + p.z.ToString("F0") + ")");
        }

        static Vector3 FindClearSpot(float x, float z)
        {
            int mask = ~0;
            foreach (var n in new[] { "Player", "Ground", "Arrow" })
            {
                int l = LayerMask.NameToLayer(n);
                if (l >= 0) mask &= ~(1 << l);
            }
            for (float r = 0f; r <= 15f; r += 1.5f)
            {
                int steps = r == 0f ? 1 : 12;
                for (int i = 0; i < steps; i++)
                {
                    float a = i * Mathf.PI * 2f / steps;
                    float px = x + Mathf.Cos(a) * r, pz = z + Mathf.Sin(a) * r;
                    var t = TerrainAt(px, pz);
                    if (t == null) continue;
                    var tp = t.GetPosition(); var size = t.terrainData.size;
                    var normal = t.terrainData.GetInterpolatedNormal((px - tp.x) / size.x, (pz - tp.z) / size.z);
                    if (normal.y < 0.9f) continue;   // steeper than ~25 degrees
                    var feet = new Vector3(px, t.SampleHeight(new Vector3(px, 0f, pz)) + tp.y, pz);
                    if (Physics.CheckCapsule(feet + Vector3.up * 0.5f, feet + Vector3.up * 1.7f, 0.5f, mask, QueryTriggerInteraction.Ignore)) continue;
                    return feet;
                }
            }
            return new Vector3(x, GroundAt(x, z), z);   // nothing clear nearby: use the spot as given
        }

        static Terrain TerrainAt(float x, float z)
        {
            foreach (var t in Terrain.activeTerrains)
            {
                var p = t.GetPosition(); var size = t.terrainData.size;
                if (x >= p.x && x <= p.x + size.x && z >= p.z && z <= p.z + size.z) return t;
            }
            return null;
        }

        static float GroundAt(float x, float z)
        {
            foreach (var t in Terrain.activeTerrains)
            {
                var p = t.GetPosition(); var size = t.terrainData.size;
                if (x >= p.x && x <= p.x + size.x && z >= p.z && z <= p.z + size.z)
                    return t.SampleHeight(new Vector3(x, 0f, z)) + p.y;
            }
            return 0f;
        }

        void Show(string text)
        {
            if (label == null)
            {
                var go = new GameObject("SpotLabel");
                label = go.AddComponent<TextMesh>();
                label.characterSize = 0.02f;
                label.fontSize = 60;
                label.anchor = TextAnchor.MiddleCenter;
                label.color = Color.white;
                var font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
                label.font = font;
                go.GetComponent<MeshRenderer>().sharedMaterial = font.material;
            }
            var cam = origin.Camera.transform;
            Vector3 fwd = cam.forward; fwd.y = 0f; fwd.Normalize();
            label.transform.position = cam.position + fwd * 2f + Vector3.down * 0.3f;
            label.transform.rotation = Quaternion.LookRotation(fwd);
            label.text = text;
            label.gameObject.SetActive(true);
            hideAt = Time.time + 3f;
        }
    }
}
