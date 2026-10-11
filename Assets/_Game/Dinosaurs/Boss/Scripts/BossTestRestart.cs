using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;

namespace BowGame
{
    // Test-scene helper: restart the boss fight without taking the headset off.
    // Press A on the right controller (or R on the keyboard) to reload the scene.
    // Only reloads once the boss or the player is dead, unless allowAnytime is ticked.
    // Also stands in for GameFlow: the fight is "on" at start and stops when the player dies.
    public class BossTestRestart : MonoBehaviour
    {
        public bool allowAnytime = false;

        InputAction restart;

        void Awake()
        {
            // No GameFlow in the test scene: act as if the fight is on (a reload after a death would otherwise keep "Dead").
            GameRefs.State = GameState.Survive;
        }

        // When the player dies, stop the fight like GameFlow would (the boss stands down).
        void OnPlayerDied() => GameRefs.State = GameState.Dead;

        void OnEnable()
        {
            GameEvents.PlayerDied += OnPlayerDied;
            restart = new InputAction("BossTestRestart", InputActionType.Button);
            restart.AddBinding("<XRController>{RightHand}/primaryButton");
            restart.AddBinding("<Keyboard>/r");
            restart.Enable();
        }

        void OnDisable()
        {
            GameEvents.PlayerDied -= OnPlayerDied;
            if (restart != null) { restart.Disable(); restart.Dispose(); restart = null; }
        }

        void Update()
        {
            if (restart == null || !restart.WasPressedThisFrame()) return;
            bool bossDead = FindFirstObjectByType<BossEnemy>() is BossEnemy b ? b.IsDead : true;
            bool playerDead = PlayerHealth.Instance != null && PlayerHealth.Instance.IsDead;
            if (allowAnytime || bossDead || playerDead)
            {
                var scene = SceneManager.GetActiveScene();
#if UNITY_EDITOR
                // Works in the editor even when the test scene isn't in Build Settings.
                UnityEditor.SceneManagement.EditorSceneManager.LoadSceneInPlayMode(scene.path, new LoadSceneParameters(LoadSceneMode.Single));
#else
                SceneManager.LoadScene(scene.buildIndex);
#endif
            }
        }
    }
}
