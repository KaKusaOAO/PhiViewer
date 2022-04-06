namespace Phi.Viewer.Utils
{
    public static class M
    {
        public static float Lerp(float a, float b, float t) => a + (b - a) * t;
    }
}