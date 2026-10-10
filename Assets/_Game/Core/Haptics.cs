using System;
using UnityEngine;

namespace BowGame
{
    // The only way to buzz the vest. Until the vest sender is plugged in, hits are logged to the Console.
    public static class Haptics
    {
        // Zahid's vest script assigns this (sends "L,200" style text over UDP port 5006).
        public static Action<HitSide, float> Sender;

        public static void Hit(HitSide side, float strength01)
        {
            strength01 = Mathf.Clamp01(strength01);
            if (Sender != null) Sender(side, strength01);
            else Debug.Log("[Haptics] " + side + " " + strength01.ToString("0.00"));
        }
    }
}
