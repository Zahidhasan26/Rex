namespace BowGame
{
    // Fast melee attacker. Quick, twitchy approach and a fast bite.
    // All behaviour lives in Dinosaur; this only sets the Maip's default numbers.
    public class Maip : Dinosaur
    {
        void Reset()
        {
            bodyHitsToKill = 3;
            moveSpeed = 6f;
            attackRange = 3.8f;
            attackCooldown = 1.2f;
            damage = 10f;
            biteDelay = 0.35f;
            weaveAmplitude = 6f;
            weaveFrequency = 0.18f;
            pauseChancePerSecond = 0.05f;
            chargeSpeedMultiplier = 1.5f;
        }
    }
}
