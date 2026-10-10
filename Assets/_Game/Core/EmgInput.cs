namespace BowGame
{
    // The one place everyone reads muscle flex strength from: 0 (relaxed) to 1 (full flex).
    // Zahid's receiver script writes it (UDP port 5005, or the controller trigger as a stand-in).
    public static class EmgInput
    {
        public static float Value;
        public static bool HardwareConnected;
    }
}
