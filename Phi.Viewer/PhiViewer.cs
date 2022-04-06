using System;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.Numerics;
using Phi.Charting;
using Phi.Resources;
using Phi.Viewer.Utils;
using Phi.Viewer.View;
using Veldrid;

namespace Phi.Viewer
{
    public class PhiViewer
    {
        private Stopwatch _pTimer = new Stopwatch();
            
        private Stopwatch _deltaTimer = new Stopwatch();
        
        public static PhiViewer Instance { get; private set; }
        
        public ViewerHost Host { get; internal set; }

        public Gui Gui { get; private set; }
        
        public Renderer Renderer { get; private set; }
        
        public ChartView Chart { get; private set; }

        public float Time { get; set; }
        
        public float PlaybackTime { get; set; }
        
        public SizeF RefScreenSize => new SizeF(1440, 1080);
        
        public bool UseUniqueSpeed { get; set; }
        
        public bool ForceRenderOffscreen { get; set; }

        public SizeF WindowSize => new SizeF(Host.Window.Width, Host.Window.Height);

        public float MaxRatio { get; set; } = 16f / 9f;

        public float CurrentAspect => WindowSize.Width / WindowSize.Height;
        
        public float RenderXPad => CurrentAspect > MaxRatio ? (WindowSize.Width - (WindowSize.Height * MaxRatio)) / 2 : 0;
        
        public float Ratio { get; set; } = 1;
        
        public Texture Background { get; set; }
        
        public bool IsPlaying { get; set; }
        
        public double PlaybackStartTime { get; set; }
        
        public float DeltaTime { get; private set; }

        public float SeekSmoothness { get; set; } = 100;

        public float NoteRatio
        {
            get
            {
                var refAspect = 16f / 9f;
                var mult = 1f;

                if (CurrentAspect < refAspect)
                {
                    mult = M.Lerp(1, CurrentAspect / refAspect, 0.8f);
                }

                return Ratio * mult;
            }   
        }

        public PhiViewer()
        {
            Instance = this;
            _pTimer.Start();
            _deltaTimer.Start();
        }

        public double MillisSinceLaunch => _pTimer.ElapsedMilliseconds;
        
        public void Init()
        {
            Renderer = new Renderer(this);
            Gui = new Gui(this);

            Chart = new ChartView(Charting.Chart.Deserialize(
                File.ReadAllText(@"D:\AppServ\www\phigros\assets\charts\rrhar\3.json")));

            Background = ImageLoader.LoadTextureFromPath(@"D:\AppServ\www\phigros\assets\charts\rrhar\bg.png");
        }

        public void Update(InputSnapshot snapshot)
        {
            DeltaTime = (float) _deltaTimer.Elapsed.TotalMilliseconds;
            _deltaTimer.Restart();
            
            Gui.Update(snapshot);

            var refAspect = RefScreenSize.Width / RefScreenSize.Height;
            var width = WindowSize.Width - RenderXPad;
            var height = WindowSize.Height;
            var aspect = width / height;

            if (aspect > refAspect)
            {
                Ratio = height / RefScreenSize.Height;
            }
            else
            {
                Ratio = width / RefScreenSize.Width;
            }

            if (IsPlaying)
            {
                PlaybackTime = (float)(MillisSinceLaunch - PlaybackStartTime);
                Time = PlaybackTime;
            }
            else
            {
                var smooth = MathF.Max(0, MathF.Min(1, DeltaTime / SeekSmoothness));
                Time = M.Lerp(Time, PlaybackTime, smooth);
            }
        }

        public void Render()
        {
            Renderer.Begin();
            
            Renderer.Transform = Matrix4x4.Identity;
            
            Renderer.EnableClip();
            Renderer.ClipRect(0, 0, WindowSize.Width, WindowSize.Height);
            
            RenderBack();
            RenderJudgeLines();
            
            Renderer.ClearClip();
            
            Renderer.Transform = Matrix4x4.Identity;
            Renderer.Scale(1, -1);
            Renderer.Translate(0, -WindowSize.Height);
            // Renderer.DrawToMainSwapchain();
            
            Gui.Render(Renderer.GraphicsDevice, Renderer.CommandList);
            Renderer.Render();
        }

        private void RenderBack()
        {
            if (Background == null) return;
            var pad = RenderXPad;
            
            Renderer.DrawTexture(Background, 0, 0, WindowSize.Width, WindowSize.Height, new TintInfo(Vector3.One, 0, 0.4f));
            
            Renderer.PushClip();
            Renderer.ClipRect(pad, 0, WindowSize.Width - pad * 2, WindowSize.Height);

            var iw = Background.Width;
            var ih = Background.Height;
            var xOffset = (WindowSize.Width - pad * 2) - WindowSize.Height / ih * iw;
            
            Renderer.DrawTexture(Background, pad, 0, WindowSize.Width - pad * 2, WindowSize.Height);
            Renderer.DrawRect(Color.FromArgb((int)(255 * 0.66), 0, 0, 0), pad, 0, WindowSize.Width - pad * 2, WindowSize.Height);
            
            Renderer.PopClip();
        }

        private void RenderJudgeLines()
        {
            var pad = RenderXPad;
            Renderer.PushClip();
            Renderer.ClipRect(pad, 0, WindowSize.Width - pad * 2, WindowSize.Height);
            
            foreach (var line in Chart.JudgeLines)
            {
                line.RenderHoldNotes();
            }
            
            foreach (var line in Chart.JudgeLines)
            {
                line.RenderShortNotes();
            }
            
            foreach (var line in Chart.JudgeLines)
            {
                line.RenderLine();
            }
            
            Renderer.PopClip();
        }
    }
}