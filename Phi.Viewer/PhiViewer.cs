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
using Color = System.Drawing.Color;

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
        
        public bool DisableGlobalClip { get; set; }
        
        public Vector2 CanvasTranslate { get; set; }

        public float CanvasScale { get; set; } = 1;

        public float Duration { get; set; } = 133000;

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
                File.ReadAllText(@"rrhar/3.json")));

            Background = ImageLoader.LoadTextureFromPath(@"rrhar/bg.png");
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
            Renderer.Translate((WindowSize.Width - WindowSize.Width * CanvasScale) / 2, (WindowSize.Height - WindowSize.Height * CanvasScale) / 2);
            Renderer.Translate(CanvasTranslate.X, CanvasTranslate.Y);
            Renderer.Scale(CanvasScale, CanvasScale);
            
            RenderBack();
            RenderJudgeLines();
            RenderUI();
            
            Renderer.PushClip();
            Renderer.ClearClip();
            Renderer.Transform = Matrix4x4.Identity;
            
            if (Renderer.GraphicsDevice.BackendType == GraphicsBackend.OpenGL ||
                Renderer.GraphicsDevice.BackendType == GraphicsBackend.OpenGLES)
            {
                Renderer.Scale(1, -1);
                Renderer.Translate(0, -WindowSize.Height);
            }
            Renderer.DrawToMainSwapchain();
            
            Renderer.PopClip();
            
            Gui.Render(Renderer.GraphicsDevice, Renderer.CommandList);
            Renderer.Render();
        }

        private void RenderBack()
        {
            if (Background == null) return;
            var pad = RenderXPad;
            
            Renderer.DrawTexture(Background, 0, 0, WindowSize.Width, WindowSize.Height, new TintInfo(Vector3.One, 0, 0.4f));
            
            Renderer.PushClip();

            var iw = Background.Width;
            var ih = Background.Height;
            var xOffset = (WindowSize.Width - pad * 2) - WindowSize.Height / ih * iw;
            
            Renderer.DrawTexture(Background, pad, 0, WindowSize.Width - pad * 2, WindowSize.Height);
            Renderer.DrawRect(Color.FromArgb((int)(255 * 0.66), 0, 0, 0), pad, 0, WindowSize.Width - pad * 2, WindowSize.Height);
            
            Renderer.PopClip();
        }

        private void RenderJudgeLines()
        {
            Renderer.CommandList.PushDebugGroup("RenderJudgeLines()");
            
            var pad = RenderXPad;
            Renderer.PushClip();
            if (!DisableGlobalClip)
            {
                Renderer.ClipRect(pad, 0, WindowSize.Width - pad * 2, WindowSize.Height);
            }

            Renderer.CommandList.PushDebugGroup("RenderJudgeLines() -> RenderHoldNotes()");
            foreach (var line in Chart.JudgeLines)
            {
                line.RenderNotes(n => n is HoldNoteView);
            }
            Renderer.CommandList.PopDebugGroup();
            
            Renderer.CommandList.PushDebugGroup("RenderJudgeLines() -> RenderShortNotes()");
            foreach (var line in Chart.JudgeLines)
            {
                line.RenderNotes(n => !(n is HoldNoteView));
            }
            Renderer.CommandList.PopDebugGroup();
            
            Renderer.CommandList.PushDebugGroup("RenderJudgeLines() -> RenderLine()");
            foreach (var line in Chart.JudgeLines)
            {
                line.RenderLine();
            }
            Renderer.CommandList.PopDebugGroup();
            
            Renderer.PopClip();
            
            Renderer.CommandList.PopDebugGroup();
        }

        private void RenderUI()
        {
            var pad = RenderXPad;
            var ratio = Ratio * 1.25f;
            var cw = WindowSize.Width - pad * 2;
            var ch = WindowSize.Height;

            Renderer.DrawRect(Color.White, pad + 30 * ratio, ch - 62 * ratio, 7.5f * ratio, 35 * ratio);
            
            var title = "Rrhar'il";
            var size = Renderer.MeasureText(title, 28 * ratio);
            var sScale = size.Width > 545 * ratio ? 545 * ratio / size.Width : 1;
            var textYOff = size.Height / 2 * sScale;
            Renderer.DrawText(title, Color.White, 28 * ratio * sScale, pad + 50 * ratio, ch - 45 * ratio + textYOff);

            var diff = "SP Lv.?";
            size = Renderer.MeasureText(diff, 28 * ratio);
            textYOff = size.Height / 2 * sScale;
            Renderer.DrawText(diff, Color.White, 28 * ratio, cw + pad - 40 * ratio - size.Width, ch - 45 * ratio + textYOff);
        }
    }
}