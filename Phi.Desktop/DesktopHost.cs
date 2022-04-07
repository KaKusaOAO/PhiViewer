using Phi.Viewer;
using Veldrid;
using Veldrid.Sdl2;
using Veldrid.StartupUtilities;

namespace Phi.Desktop
{
    public class DesktopHost : ViewerHost
    {
        public DesktopHost()
        {
            var window = new Sdl2Window("PhiViewer", 100, 100, 1280, 720, 
                SDL_WindowFlags.Shown | SDL_WindowFlags.Resizable | SDL_WindowFlags.OpenGL, false);

            var device = VeldridStartup.CreateGraphicsDevice(window, new GraphicsDeviceOptions {
                PreferStandardClipSpaceYDirection = true,
                PreferDepthRangeZeroToOne = true
            }, GraphicsBackend.OpenGL);
            
            Window = new Sdl2DesktopWindow(window, device);
        }
        
        public override void Run(PhiViewer viewer)
        {
            base.Run(viewer);
            
            while (Window.Exists)
            {
                var snapshot = Window.PumpEvents();
                viewer.Update(snapshot);
                viewer.Render();
            }
        }
    }
}