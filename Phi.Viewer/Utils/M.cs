namespace Phi.Viewer.Utils
{
    public static class M
    {
        public static float Lerp(float a, float b, float t) => a + (b - a) * t;

        public static float Clamp(float n, float min, float max) => n < min ? min : n > max ? max : n;
    }
}