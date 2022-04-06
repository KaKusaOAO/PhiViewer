using Phi.Viewer;

namespace Phi.Desktop
{
    public static class Program
    {
        public static void Main(string[] args)
        {
            using var host = new DesktopHost();
            host.Run(new PhiViewer());
        }
    }
}