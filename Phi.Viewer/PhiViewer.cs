using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.Numerics;
using System.Timers;
using ManagedBass;
using Phi.Charting;
using Phi.Resources;
using Phi.Viewer.Audio;
using Phi.Viewer.Graphics;
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
        
        public Texture BlurredBackground { get; set; }
        
        public Texture BlurredBackgroundDepth { get; set; }
        
        public PipelineProfile BlurredProfile { get; set; }
        
        public bool IsPlaying { get; set; }
        
        public float DeltaTime { get; private set; }

        public float SeekSmoothness { get; set; } = 100;
        
        public bool DisableGlobalClip { get; set; }
        
        public Vector2 CanvasTranslate { get; set; }

        public float CanvasScale { get; set; } = 1;
        
        public float AudioOffset { get; set; }

        public float GetCalculatedAudioOffset()
        {
            var offset = AudioOffset + Chart.Model.Offset;
            return Bass.Info.Latency + offset / MusicPlayer.PlaybackRate;
        }

        public AudioPlayer MusicPlayer { get; } = new AudioPlayer();
        
        public bool IsLoopEnabled { get; set; }

        public bool EnableClickSound { get; set; } = true;
        
        public bool EnableParticles { get; set; } = true;

        public List<AnimatedObject> AnimatedObjects { get; } = new List<AnimatedObject>();

        private object _animObjectsLock = new object();

        private Timer _fixedUpdateTimer = new Timer();

        public Random Random { get; } = new Random();

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

        public double MillisSinceLaunch => _pTimer.Elapsed.TotalMilliseconds;
        
        public void Init()
        {
            Renderer = new Renderer(this);
            Gui = new Gui(this);

            Chart = new ChartView(Charting.Chart.Deserialize(
                File.ReadAllText(@"rrhar/3.json")));

            Background = ImageLoader.LoadTextureFromPath(@"rrhar/bg.png");

            Bass.GlobalStreamVolume = 3000;
            MusicPlayer.LoadFromPath(@"rrhar/base.wav");

            _fixedUpdateTimer.Elapsed += (o, e) =>
            {
                lock (_animObjectsLock)
                {
                    foreach (var obj in AnimatedObjects)
                    {
                        obj.FixedUpdate();
                    }
                }
            };
            _fixedUpdateTimer.Interval = 5;
            _fixedUpdateTimer.Start();
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
                var time = DeltaTime * MusicPlayer.PlaybackRate + PlaybackTime;
                var audioTime = MusicPlayer.PlaybackTime + GetCalculatedAudioOffset() / MusicPlayer.PlaybackRate;

                if (Math.Abs(time - audioTime) > 20)
                {
                    time = audioTime;
                }
                
                if (MusicPlayer.PlaybackTime >= MusicPlayer.Duration)
                {
                    MusicPlayer.Stop();
                    IsPlaying = IsLoopEnabled;

                    if (IsLoopEnabled)
                    {
                        MusicPlayer.Play();
                    }
                }

                PlaybackTime = Math.Min(time, MusicPlayer.Duration);
                Time = PlaybackTime;
            }
            else
            {
                var smooth = MathF.Max(0, MathF.Min(1, DeltaTime / SeekSmoothness));
                Time = M.Lerp(Time, PlaybackTime, smooth);
            }
        }

        public void AddAnimatedObject(AnimatedObject obj)
        {
            lock (_animObjectsLock)
            {
                AnimatedObjects.Add(obj);
            }
        }
        
        public void Render()
        {
            Renderer.Begin();
            Renderer.SetCurrentPipelineProfile(Renderer.BasicNormal);
            Renderer.ClearBuffers();
            Renderer.SetFullViewport();

            Renderer.Transform = Matrix4x4.Identity;
            Renderer.Translate((WindowSize.Width - WindowSize.Width * CanvasScale) / 2, (WindowSize.Height - WindowSize.Height * CanvasScale) / 2);
            Renderer.Translate(CanvasTranslate.X, CanvasTranslate.Y);
            Renderer.Scale(CanvasScale, CanvasScale);
            
            RenderBack();
            RenderJudgeLines();

            lock (_animObjectsLock)
            {
                var removal = new List<AnimatedObject>();
                foreach (var obj in AnimatedObjects)
                {
                    obj.Update();
                    if (obj.NotNeeded)
                    {
                        removal.Add(obj);
                    }
                }

                foreach (var obj in removal)
                {
                    AnimatedObjects.Remove(obj);
                }
            }
            
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

            var count = 0;
            var combo = 0;
            var score = 0;

            if (Chart != null)
            {
                foreach (var line in Chart.JudgeLines)
                {
                    foreach (var n in line.NotesAbove)
                    {
                        if (n.IsCleared) combo++;
                        count++;
                    }
                    
                    foreach (var n in line.NotesBelow)
                    {
                        if (n.IsCleared) combo++;
                        count++;
                    }
                }
            }

            var scoreStr = "";
            var maxScore = 1000000;
            score = count == 0 ? 0 : (int) Math.Round(maxScore * ((double)combo / count));
            for (var i = 0; i <= MathF.Log10(maxScore) - 2 - MathF.Floor(score == 0 ? 0 : MathF.Log10(score)) + 1; i++)
            {
                scoreStr += "0";
            }
            scoreStr += score;
            
            // -- Play bar
            var offset = Chart != null ? Chart.Model.Offset * 1000 : 0;
            offset += AudioOffset;
            Renderer.DrawRect(Color.FromArgb((int) (0.3 * 255), 255, 255, 255), pad, 0,
                (Time - offset) / MusicPlayer.Duration * (WindowSize.Width - pad * 2), 10 * Ratio);
            Renderer.DrawRect(Color.FromArgb((int) (0.3 * 255), 255, 255, 255), pad, 0,
                MusicPlayer.PlaybackTime / MusicPlayer.Duration * (WindowSize.Width - pad * 2), 10 * Ratio);
            Renderer.DrawRect(Color.White, (Time - offset) / MusicPlayer.Duration * (WindowSize.Width - pad * 2) + pad, 0,
                2.5f * Ratio + MathF.Max(0, (MusicPlayer.PlaybackTime / MusicPlayer.Duration - (Time - offset) / MusicPlayer.Duration) * (WindowSize.Width - pad * 2)),
                10 * Ratio);
            
            // -- Pause button
            Renderer.DrawRect(Color.FromArgb(0x88, 0, 0, 0), 30 * Ratio + pad, 32 * Ratio, 9 * Ratio, 29 * Ratio);
            Renderer.DrawRect(Color.FromArgb(0x88, 0, 0, 0), 47 * Ratio + pad, 32 * Ratio, 9 * Ratio, 29 * Ratio);
            Renderer.DrawRect(Color.White, 26 * Ratio + pad, 28 * Ratio, 9 * Ratio, 29 * Ratio);
            Renderer.DrawRect(Color.White, 43 * Ratio + pad, 28 * Ratio, 9 * Ratio, 29 * Ratio);
            
            // -- Song title
            Renderer.DrawRect(Color.White, pad + 30 * ratio, ch - 62 * ratio, 7.5f * ratio, 35 * ratio);
            
            var title = "Rrhar'il";
            var size = Renderer.MeasureText(title, 28 * ratio);
            var sScale = size.Width > 545 * ratio ? 545 * ratio / size.Width : 1;
            var textYOff = size.Height / 2 * sScale;
            Renderer.DrawText(title, Color.White, 28 * ratio * sScale, pad + 50 * ratio, ch - 45 * ratio + textYOff);

            // -- Song difficulty & level
            var diff = "SP Lv.?";
            size = Renderer.MeasureText(diff, 28 * ratio);
            textYOff = size.Height / 2 * sScale;
            Renderer.DrawText(diff, Color.White, 28 * ratio, cw + pad - 40 * ratio - size.Width, ch - 45 * ratio + textYOff);
            
            // -- Combo
            if (combo >= 3)
            {
                size = Renderer.MeasureText("COMBO", 22 * Ratio);
                Renderer.DrawText("COMBO", Color.White, 22 * Ratio, (cw - size.Width) / 2, 72 * ratio);
                size = Renderer.MeasureText($"{combo}", 58 * Ratio);
                Renderer.DrawText($"{combo}", Color.White, 58 * Ratio, (cw - size.Width) / 2, 50 * ratio);
            }
            
            // -- Score
            size = Renderer.MeasureText(scoreStr, 36 * Ratio);
            Renderer.DrawText(scoreStr, Color.White, 36 * Ratio, cw + pad - 30 * Ratio - size.Width, 45 * ratio);
        }
    }
}