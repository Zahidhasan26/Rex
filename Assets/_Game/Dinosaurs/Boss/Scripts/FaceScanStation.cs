using System.Collections;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.InputSystem;

namespace BowGame
{
    // Pre-game face scan, run on the PC before the player puts the headset on.
    // Flow: consent screen -> live camera with an oval guide -> 3-2-1 photo -> review -> keep or retake.
    // Nothing happens until the person agrees. The photo is kept only in memory (FaceScanStore) for
    // this play session: it is not saved to disk or sent anywhere. Skipping gives the boss a default mask.
    //
    // Keys (operator at the PC):  Y agree   N skip   Space/C take photo   R retake   Enter keep   Backspace forget scan
    // The same steps are public methods (Agree, Skip, TakePhoto, Retake, Keep, Forget) for UI buttons.
    public class FaceScanStation : MonoBehaviour
    {
        public enum Step { Off, Consent, Live, Countdown, Review, Done }

        [Header("Camera")]
        [Tooltip("Leave empty for the default webcam.")]
        public string deviceName = "";
        public int requestedWidth = 1280;
        public int requestedHeight = 720;

        [Header("Guide (fractions of the camera image height)")]
        [Range(0.3f, 0.95f)] public float guideHeight = 0.72f;
        [Tooltip("Oval width divided by height.")]
        [Range(0.5f, 1f)] public float guideAspect = 0.78f;
        [Tooltip("Where the eye line sits, from the top of the oval (0..1).")]
        [Range(0.2f, 0.6f)] public float eyeLine = 0.42f;
        public int outputWidth = 384;
        public int outputHeight = 480;
        public float countdownSeconds = 3f;

        [Header("Flow")]
        public bool showOnStart = true;
        [Tooltip("If a scan is already in memory (e.g. after restarting a test scene), skip the booth and finish straight away.")]
        public bool skipIfAlreadyScanned = true;
        [Tooltip("Optional quad/plane that also shows the live camera (e.g. a mirror in the lobby).")]
        public Renderer previewScreen;
        [Tooltip("Called after the scan is kept or skipped.")]
        public UnityEvent onFinished;

        public Step CurrentStep { get; private set; } = Step.Off;

        WebCamTexture cam;
        Texture2D captured;
        Texture2D overlay;
        float countdownEnd;
        string message = "";

        void Start()
        {
            if (!showOnStart) return;
            if (skipIfAlreadyScanned && FaceScanStore.HasFace) { CurrentStep = Step.Done; onFinished?.Invoke(); return; }
            Begin();
        }

        public void Begin()
        {
            message = "";
            CurrentStep = Step.Consent;
        }

        // ---------- steps ----------

        public void Agree()
        {
            if (CurrentStep != Step.Consent) return;
            if (WebCamTexture.devices.Length == 0)
            {
                message = "No camera found. The boss will wear the default mask.";
                Finish();
                return;
            }
            string dev = string.IsNullOrEmpty(deviceName) ? WebCamTexture.devices[0].name : deviceName;
            cam = new WebCamTexture(dev, requestedWidth, requestedHeight, 30);
            cam.Play();
            if (previewScreen != null) previewScreen.material.mainTexture = cam;
            CurrentStep = Step.Live;
        }

        public void Skip()
        {
            if (CurrentStep == Step.Off || CurrentStep == Step.Done) return;
            DiscardCapture();
            Finish();
        }

        public void TakePhoto()
        {
            if (CurrentStep != Step.Live || cam == null || !cam.isPlaying) return;
            countdownEnd = Time.unscaledTime + countdownSeconds;
            CurrentStep = Step.Countdown;
        }

        public void Retake()
        {
            if (CurrentStep != Step.Review) return;
            DiscardCapture();
            CurrentStep = Step.Live;
        }

        public void Keep()
        {
            if (CurrentStep != Step.Review || captured == null) return;
            FaceScanStore.Set(captured);   // the store owns it now
            captured = null;
            Finish();
        }

        // Removes the scan from memory at any time (the boss goes back to the default mask).
        public void Forget()
        {
            DiscardCapture();
            FaceScanStore.Clear();
            message = "Scan removed.";
        }

        void Finish()
        {
            StopCamera();
            CurrentStep = Step.Done;
            onFinished?.Invoke();
        }

        // ---------- per frame ----------

        void Update()
        {
            var kb = Keyboard.current;
            if (kb != null)
            {
                if (kb.backspaceKey.wasPressedThisFrame) Forget();
                switch (CurrentStep)
                {
                    case Step.Consent:
                        if (kb.yKey.wasPressedThisFrame) Agree();
                        else if (kb.nKey.wasPressedThisFrame) Skip();
                        break;
                    case Step.Live:
                        if (kb.spaceKey.wasPressedThisFrame || kb.cKey.wasPressedThisFrame) TakePhoto();
                        else if (kb.nKey.wasPressedThisFrame) Skip();
                        break;
                    case Step.Review:
                        if (kb.enterKey.wasPressedThisFrame || kb.numpadEnterKey.wasPressedThisFrame) Keep();
                        else if (kb.rKey.wasPressedThisFrame) Retake();
                        else if (kb.nKey.wasPressedThisFrame) Skip();
                        break;
                }
            }

            if (CurrentStep == Step.Countdown && Time.unscaledTime >= countdownEnd)
            {
                captured = CaptureCrop();
                CurrentStep = captured != null ? Step.Review : Step.Live;
            }
        }

        // Crops the oval guide area out of the current camera frame (unmirrored, as others will see it).
        Texture2D CaptureCrop()
        {
            if (cam == null || cam.width < 16) return null;
            int w = cam.width, h = cam.height;
            Color32[] src = cam.GetPixels32();
            bool flipY = cam.videoVerticallyMirrored;

            float cropH = guideHeight * h;
            float cropW = cropH * guideAspect;
            float x0 = (w - cropW) * 0.5f, y0 = (h - cropH) * 0.5f;

            var tex = new Texture2D(outputWidth, outputHeight, TextureFormat.RGBA32, false);
            tex.wrapMode = TextureWrapMode.Clamp;
            tex.name = "FaceScan (memory only)";
            var dst = new Color32[outputWidth * outputHeight];
            for (int y = 0; y < outputHeight; y++)
            {
                float sy = y0 + (y + 0.5f) / outputHeight * cropH;
                if (flipY) sy = h - 1 - sy;
                for (int x = 0; x < outputWidth; x++)
                {
                    float sx = x0 + (x + 0.5f) / outputWidth * cropW;
                    dst[y * outputWidth + x] = Bilinear(src, w, h, sx, sy);
                }
            }
            tex.SetPixels32(dst);
            tex.Apply(false, false);
            return tex;
        }

        static Color32 Bilinear(Color32[] px, int w, int h, float x, float y)
        {
            x = Mathf.Clamp(x - 0.5f, 0, w - 1.001f);
            y = Mathf.Clamp(y - 0.5f, 0, h - 1.001f);
            int ix = (int)x, iy = (int)y;
            float fx = x - ix, fy = y - iy;
            Color32 a = px[iy * w + ix], b = px[iy * w + ix + 1];
            Color32 c = px[(iy + 1) * w + ix], d = px[(iy + 1) * w + ix + 1];
            return Color32.Lerp(Color32.Lerp(a, b, fx), Color32.Lerp(c, d, fx), fy);
        }

        void DiscardCapture()
        {
            if (captured != null) Destroy(captured);
            captured = null;
        }

        void StopCamera()
        {
            if (cam != null)
            {
                cam.Stop();
                Destroy(cam);
                cam = null;
            }
            if (previewScreen != null) previewScreen.material.mainTexture = null;
        }

        void OnDisable()
        {
            StopCamera();
            DiscardCapture();
        }

        void OnDestroy()
        {
            if (overlay != null) Destroy(overlay);
        }

        // ---------- on-screen overlay for the PC monitor ----------

        GUIStyle big, small;

        void OnGUI()
        {
            if (CurrentStep == Step.Off || CurrentStep == Step.Done) return;
            if (big == null)
            {
                big = new GUIStyle(GUI.skin.label) { fontSize = 28, alignment = TextAnchor.MiddleCenter, wordWrap = true };
                small = new GUIStyle(GUI.skin.label) { fontSize = 18, alignment = TextAnchor.MiddleCenter, wordWrap = true };
            }
            GUI.color = new Color(0, 0, 0, 0.85f);
            GUI.DrawTexture(new Rect(0, 0, Screen.width, Screen.height), Texture2D.whiteTexture);
            GUI.color = Color.white;

            float W = Screen.width, H = Screen.height;
            switch (CurrentStep)
            {
                case Step.Consent:
                    GUI.Label(new Rect(W * 0.1f, H * 0.18f, W * 0.8f, H * 0.12f), "Face scan for the final boss", big);
                    GUI.Label(new Rect(W * 0.15f, H * 0.32f, W * 0.7f, H * 0.3f),
                        "We can take one photo of your face and put it on the final boss.\n\n" +
                        "The photo stays in this computer's memory for this play session only. " +
                        "It is not saved to disk and not uploaded anywhere, and it is deleted when the game closes.\n\n" +
                        "You can say no: the boss will wear a mask instead.", small);
                    GUI.Label(new Rect(W * 0.1f, H * 0.68f, W * 0.8f, H * 0.1f), "Y = I agree, take my photo        N = no thanks", big);
                    break;

                case Step.Live:
                case Step.Countdown:
                    Rect view = CameraRect();
                    if (cam != null) GUI.DrawTextureWithTexCoords(view, cam, new Rect(1, 0, -1, 1));   // mirrored, like a selfie
                    GUI.DrawTexture(view, Overlay());
                    string top = CurrentStep == Step.Countdown
                        ? Mathf.CeilToInt(countdownEnd - Time.unscaledTime).ToString()
                        : "Fit your face inside the oval, eyes on the line. Look straight ahead.";
                    GUI.Label(new Rect(0, H * 0.02f, W, H * 0.08f), top, big);
                    GUI.Label(new Rect(0, H * 0.9f, W, H * 0.08f), "Space = take photo (3 s countdown)      N = skip", small);
                    break;

                case Step.Review:
                    float rh = H * 0.62f, rw = rh * outputWidth / outputHeight;
                    if (captured != null) GUI.DrawTexture(new Rect((W - rw) * 0.5f, H * 0.14f, rw, rh), captured);
                    GUI.Label(new Rect(0, H * 0.03f, W, H * 0.08f), "Use this photo?", big);
                    GUI.Label(new Rect(0, H * 0.82f, W, H * 0.1f), "Enter = keep it      R = retake      N = skip (mask)", big);
                    break;
            }
            if (!string.IsNullOrEmpty(message)) GUI.Label(new Rect(0, H * 0.94f, W, H * 0.05f), message, small);
        }

        Rect CameraRect()
        {
            float aspect = cam != null && cam.height > 16 ? (float)cam.width / cam.height : 16f / 9f;
            float h = Screen.height * 0.78f, w = h * aspect;
            if (w > Screen.width * 0.95f) { w = Screen.width * 0.95f; h = w / aspect; }
            return new Rect((Screen.width - w) * 0.5f, (Screen.height - h) * 0.5f, w, h);
        }

        // Darkens everything outside the oval guide and marks the eye line.
        Texture2D Overlay()
        {
            float aspect = cam != null && cam.height > 16 ? (float)cam.width / cam.height : 16f / 9f;
            int oh = 256, ow = Mathf.RoundToInt(oh * aspect);
            if (overlay != null && overlay.width == ow) return overlay;
            if (overlay != null) Destroy(overlay);
            overlay = new Texture2D(ow, oh, TextureFormat.RGBA32, false);
            float ry = guideHeight * 0.5f * oh, rx = ry * guideAspect;
            float cx = ow * 0.5f, cy = oh * 0.5f;
            float eyeY = cy + ry - eyeLine * 2f * ry;   // texture rows go bottom-up
            var px = new Color32[ow * oh];
            for (int y = 0; y < oh; y++)
                for (int x = 0; x < ow; x++)
                {
                    float dx = (x - cx) / rx, dy = (y - cy) / ry;
                    float d = Mathf.Sqrt(dx * dx + dy * dy);
                    Color32 c = new Color32(0, 0, 0, 150);
                    if (d < 1f) c = new Color32(0, 0, 0, 0);
                    if (Mathf.Abs(d - 1f) < 0.015f) c = new Color32(255, 255, 255, 230);
                    if (d < 0.8f && Mathf.Abs(y - eyeY) < 0.6f && ((x / 4) % 2 == 0)) c = new Color32(120, 220, 255, 200);
                    px[y * ow + x] = c;
                }
            overlay.SetPixels32(px);
            overlay.Apply();
            return overlay;
        }
    }
}
