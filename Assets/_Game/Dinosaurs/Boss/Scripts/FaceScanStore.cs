using System;
using UnityEngine;

namespace BowGame
{
    // Holds the player's face scan for the current play session only.
    // The picture lives in memory: it is never written to disk, never uploaded, and is
    // destroyed when it is replaced, cleared, or the game quits.
    public static class FaceScanStore
    {
        public static Texture2D Face { get; private set; }
        public static bool HasFace => Face != null;

        // Raised when a scan is set or cleared, so the boss head can update live.
        public static event Action Changed;

        public static void Set(Texture2D face)
        {
            if (Face != null && Face != face) UnityEngine.Object.Destroy(Face);
            Face = face;
            Changed?.Invoke();
        }

        public static void Clear()
        {
            if (Face != null) UnityEngine.Object.Destroy(Face);
            Face = null;
            Changed?.Invoke();
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetOnLoad()
        {
            Face = null;
            Changed = null;
            Application.quitting -= OnQuit;
            Application.quitting += OnQuit;
        }

        static void OnQuit()
        {
            if (Face != null) UnityEngine.Object.Destroy(Face);
            Face = null;
        }
    }
}
