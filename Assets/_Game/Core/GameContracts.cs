using System;
using UnityEngine;

// Shared contracts for the whole team. Do not change without team agreement (see ARCHITECTURE.md section 6).
namespace BowGame
{
    public enum GameState { Calibrate, Countdown, Survive, ActOneComplete, Dead }
    public enum HitZone   { Head, Body, Legs }
    public enum HitSide   { Left, Right, Front }

    // Implemented by the dinosaur script (Halar and Zach). Called by Zahid's arrow.
    public interface IDamageable
    {
        void TakeHit(HitZone zone, float drawStrength01, Vector3 hitPoint);
    }

    public static class GameEvents
    {
        public static event Action<GameState> StateChanged;      // raised by GameFlow
        public static event Action<float, Vector3> PlayerHit;    // damage, world position of attacker (raised by dinosaurs)
        public static event Action PlayerDied;                   // raised by player health
        public static event Action<GameObject> DinoDied;         // raised by dinosaurs

        public static void RaiseStateChanged(GameState s)          => StateChanged?.Invoke(s);
        public static void RaisePlayerHit(float dmg, Vector3 from) => PlayerHit?.Invoke(dmg, from);
        public static void RaisePlayerDied()                       => PlayerDied?.Invoke();
        public static void RaiseDinoDied(GameObject dino)          => DinoDied?.Invoke(dino);
    }

    public static class GameRefs
    {
        public static Transform PlayerHead;     // set by the player rig, or by DummyPlayer in test scenes
        public static GameState State;          // set by GameFlow
        public static float StateTimeLeft;      // set by GameFlow every frame
    }
}
