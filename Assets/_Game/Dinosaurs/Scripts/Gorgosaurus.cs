namespace BowGame
{
    // Slower, tougher melee attacker. Heavy, lazy approach and a slow, hard bite.
    // All behaviour lives in Dinosaur; this only sets the Gorgosaurus's default numbers.
    public class Gorgosaurus : Dinosaur
    {
        void Reset()
        {
            bodyHitsToKill = 5;
            moveSpeed = 3f;
            turnSpeed = 180f;
            attackRange = 5f;
            attackCooldown = 2.2f;
            damage = 15f;
            biteDelay = 0.6f;
            recoverTime = 0.8f;
            weaveAmplitude = 4f;
            weaveFrequency = 0.08f;
            pauseChancePerSecond = 0.08f;
            chargeDistance = 12f;
            chargeSpeedMultiplier = 1.3f;
        }
    }
}
