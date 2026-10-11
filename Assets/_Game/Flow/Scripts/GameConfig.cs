using UnityEngine;

namespace BowGame
{
    // Every number for Act 1 in one place, so tuning never needs a code change.
    // The asset lives at Assets/_Game/Flow/GameConfig.asset.
    [CreateAssetMenu(menuName = "BowGame/Game Config", fileName = "GameConfig")]
    public class GameConfig : ScriptableObject
    {
        [Header("Round")]
        [Tooltip("Length of the fight in seconds, including the alpha Maip.")]
        public float roundSeconds = 45f;
        public float countdownSeconds = 3f;
        public float playerHealth = 250f;

        [Header("Tutorial")]
        public float controlsSeconds = 7f;
        public float relaxSeconds = 3f;
        public float flexSeconds = 3f;
        public int practiceShots = 3;
        [Tooltip("Seconds of joystick walking forward, then backward, before the fight (not shown to the player).")]
        public float walkForwardSeconds = 4f;
        public float walkBackSeconds = 3f;
        [Tooltip("Restart goes straight to the ring and the countdown.")]
        public bool skipTutorialOnRestart = true;

        [Header("Places (world metres; yaw 0 = north, 90 = east)")]
        public Vector2 startSpot = new Vector2(-600f, -150f);
        public float startYaw = 270f;
        public Vector2 ringSpot = new Vector2(-587.8f, -132.3f);
        [Tooltip("Direction the dinosaurs come from.")]
        public float frontYaw = 7f;
        public float ringRadius = 2.5f;
        [Tooltip("How close (degrees) the player must face the front before the countdown starts.")]
        public float facingTolerance = 35f;
        [Tooltip("During the fight the player can't walk further than this from the ring.")]
        public float playAreaRadius = 25f;

        [Header("Practice targets (in front of the start spot)")]
        public float targetDistance = 14f;
        public float targetSpacing = 4f;

        [Header("Music (2D, looping). Leave empty for silence.")]
        public AudioClip calmMusic;
        public AudioClip battleMusic;
        [Range(0f, 1f)] public float musicVolume = 0.5f;
        public float musicFade = 2f;
    }
}
