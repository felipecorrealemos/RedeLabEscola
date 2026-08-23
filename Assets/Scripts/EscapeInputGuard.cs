public static class EscapeInputGuard
{
    private static int consumedFrame = -1;

    public static bool WasConsumedThisFrame => consumedFrame == UnityEngine.Time.frameCount;

    public static void Consume()
    {
        consumedFrame = UnityEngine.Time.frameCount;
    }
}
