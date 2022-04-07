using System.Diagnostics;
using System.Numerics;
using ImGuiNET;
using Veldrid;

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
                ImGui.SliderFloat("Playback Time", ref p, 0, 157760);
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

                bl = viewer.DisableGlobalClip;
                ImGui.Checkbox("Disable Global Clip", ref bl);
                viewer.DisableGlobalClip = bl;

                var vec2 = viewer.CanvasTranslate;
                ImGui.DragFloat2("Canvas Translate", ref vec2);
                viewer.CanvasTranslate = vec2;
                
                if (ImGui.Button("Reset Translate"))
                {
                    viewer.CanvasTranslate = Vector2.Zero;
                }

                p = viewer.CanvasScale;
                ImGui.SliderFloat("Canvas Scale", ref p, 0.5f, 2f);
                viewer.CanvasScale = p;

                if (ImGui.Button("Reset Scale"))
                {
                    viewer.CanvasScale = 1;
                }
                
                ImGui.End();
            }
            renderer.Render(device, list);
        }
    }
}