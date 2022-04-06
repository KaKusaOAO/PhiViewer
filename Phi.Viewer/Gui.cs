using System.Diagnostics;
using System.Numerics;
using ImGuiNET;
using Phi.Viewer.View;
using Veldrid;
using Veldrid.Utilities;

namespace Phi.Viewer
{
    public class Gui
    {
        private PhiViewer viewer;
        
        private ImGuiRenderer renderer;

        private Stopwatch _stopwatch = new Stopwatch();

        public Gui(PhiViewer viewer)
        {
            this.viewer = viewer;
            var window = viewer.Host.Window;
            renderer = new ImGuiRenderer(window.GraphicsDevice, 
                window.GraphicsDevice.MainSwapchain.Framebuffer.OutputDescription, 
                (int) window.Width, (int) window.Height);

            _stopwatch.Start();
        }

        public void Update(InputSnapshot snapshot)
        {
            var delta = _stopwatch.Elapsed.TotalSeconds;
            _stopwatch.Restart();
            
            var window = viewer.Host.Window;
            renderer.WindowResized((int) window.Width, (int) window.Height);
            renderer.Update((float) delta, snapshot);
        }

        public void Render(GraphicsDevice device, CommandList list)
        {
            list.SetFramebuffer(device.SwapchainFramebuffer);
            
            if (ImGui.Begin("Control"))
            {
                var p = viewer.PlaybackTime;
                ImGui.SliderFloat("Playback Time", ref p, 0, 133000);
                viewer.PlaybackTime = p;
                
                ImGui.SameLine();
                if (ImGui.Button("Play"))
                {
                    viewer.PlaybackStartTime = viewer.MillisSinceLaunch - viewer.PlaybackTime;
                    viewer.IsPlaying = true;
                }
                
                ImGui.SameLine();
                if (ImGui.Button("Stop"))
                {
                    viewer.IsPlaying = false;
                }

                var bl = viewer.ForceRenderOffscreen;
                ImGui.Checkbox("Force Render Offscreen", ref bl);
                viewer.ForceRenderOffscreen = bl;
                
                bl = viewer.UseUniqueSpeed;
                ImGui.Checkbox("Use Unique Speed", ref bl);
                viewer.UseUniqueSpeed = bl;
                
                ImGui.End();
            }
            
            if (ImGui.Begin("Game"))
            {
                var factory = device.ResourceFactory;
                var texture = viewer.Renderer.RenderTargetTexture;
                ImGui.Image(renderer.GetOrCreateImGuiBinding(factory, texture),
                    new Vector2(texture.Width / 1.5f,  texture.Height / 1.5f),
                    new Vector2(0, 1), new Vector2(1, 0));
                ImGui.SetWindowSize(new Vector2(texture.Width / 1.5f + 10,  texture.Height / 1.5f + 40));
                ImGui.End();
            }
            renderer.Render(device, list);
        }
    }
}