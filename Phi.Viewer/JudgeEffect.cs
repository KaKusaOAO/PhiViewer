using System;
using System.Collections.Generic;
using System.Drawing;
using System.Numerics;
using Phi.Resources;
using Phi.Viewer.Graphics;
using Phi.Viewer.Utils;
using Veldrid;

namespace Phi.Viewer
{
    public class JudgeEffect : AnimatedObject
    {
        public class Particle
        {
            public double SpawnTime { get; set; }
            public Vector2 Position { get; set; }
            public Vector2 Motion { get; set; }
        }
        
        public float X { get; set; }
        public float Y { get; set; }
        public List<Particle> Particles { get; set; } = new List<Particle>();
        public double StartTime { get; set; }

        private static Texture _arcTexture;
        private static Texture _circleTexture;

        static JudgeEffect()
        {
            var asm = ResourceHelper.Assembly;
            var stream = asm.GetManifestResourceStream("Phi.Resources.Textures.Circle.png");
            _circleTexture = ImageLoader.LoadTextureFromStream(stream);
            
            stream = asm.GetManifestResourceStream("Phi.Resources.Textures.Arcs.png");
            _arcTexture = ImageLoader.LoadTextureFromStream(stream);
        }
        
        /// <summary>
        /// Creates a judge effect particle.
        /// </summary>
        /// <param name="x">The X position in canvas space.</param>
        /// <param name="y">THe Y position in canvas space.</param>
        /// <param name="amount"></param>
        public JudgeEffect(float x, float y, float amount)
        {
            var viewer = PhiViewer.Instance;
            X = x / viewer.WindowSize.Width * viewer.RefScreenSize.Width;
            Y = y / viewer.WindowSize.Height * viewer.RefScreenSize.Height;
            StartTime = viewer.MillisSinceLaunch;

            var rand = viewer.Random;
            for (var i = 0; i < amount; i++)
            {
                var r = rand.NextDouble() * Math.PI * 2;
                var force = rand.NextDouble() * 7 + 7;
                Particles.Add(new Particle
                {
                    SpawnTime = StartTime,
                    Position = Vector2.Zero,
                    Motion = new Vector2(
                        (float)(Math.Cos(r) * force),
                        (float)(Math.Sin(r) * force))
                });
            }
        }

        public override void FixedUpdate()
        {
            base.FixedUpdate();
            var viewer = PhiViewer.Instance;
            foreach (var p in Particles)
            {
                p.Position += p.Motion;
                p.Motion *= MathF.Pow(0.92f, MathF.Pow(viewer.MusicPlayer.PlaybackRate, 0.275f));
            }
        }

        public override void Update()
        {
            base.Update();
            var viewer = PhiViewer.Instance;
            var renderer = viewer.Renderer;
            var w = renderer.GraphicsDevice.SwapchainFramebuffer.Width;
            var h = renderer.GraphicsDevice.SwapchainFramebuffer.Height;
            var noteRatio = viewer.NoteRatio;

            var progress = (float) (viewer.MillisSinceLaunch - StartTime) / (500 / viewer.MusicPlayer.PlaybackRate);
            var t = renderer.Transform;

            var size = 100 * noteRatio;
            var alpha = MathF.Pow(MathF.Max(0, 1 - progress), 1.1f);
            var lineWidth = 4 * viewer.NoteRatio;
            var color = Color.FromArgb((int)(alpha * 255), 0xff, 0xee, 0xaa);
            
            renderer.Translate(X * w / viewer.RefScreenSize.Width, Y * h / viewer.RefScreenSize.Height);

            var s2 = size * (0.75f + 0.25f * (1 - MathF.Pow(1 - progress, 5)));
            renderer.StrokeRect(color, -s2, -s2, s2 * 2, s2 * 2, lineWidth);
            renderer.Rotate(MathF.PI / 4);

            var sThickProg = MathF.Pow(MathF.Max(0, 1 - progress), 2);
            var sThick = 48 * sThickProg;
            lineWidth = sThick * sThickProg * noteRatio;
            var s3 = size - sThick * 0.5f * sThickProg * noteRatio;
            s3 *= 0.8f + 0.3f * MathF.Pow(progress, 0.25f);
            alpha *= 0.125f;
            color = Color.FromArgb((int)(alpha * 255), 0xff, 0xee, 0xaa);
            renderer.StrokeRect(color, -s3, -s3, s3 * 2, s3 * 2, lineWidth);
            renderer.Rotate(-MathF.PI / 4);

            lineWidth = 6 * noteRatio;
            alpha = MathF.Pow(sThickProg, 0.33f);
            color = Color.FromArgb((int)(alpha * 255), 0xff, 0xee, 0xaa);
            
            var offset = (1 - MathF.Pow(1 - progress, 3)) * (MathF.PI / 4) - MathF.PI / 4;
            // renderer.StrokeArc(color, 0, 0, s2 * 0.9f, offset, offset - MathF.PI / 2, lineWidth, true);
            // renderer.StrokeArc(color, 0, 0, s2 * 0.9f, offset + MathF.PI / 2, offset + MathF.PI, lineWidth);
            var arcRadius = s2 * 0.9f;
            var t2 = renderer.Transform;
            renderer.Rotate(offset);
            renderer.DrawTexture(_arcTexture, -arcRadius, -arcRadius, arcRadius * 2, arcRadius * 2,
                new TintInfo(new Vector3(color.R / 255f, color.G / 255f, color.B / 255f), 0, color.A / 255f));
            renderer.Transform = t2;

            alpha = sThickProg;
            color = Color.FromArgb((int)(alpha * 255), 0xff, 0xee, 0xaa);
            // renderer.DrawSectorSmooth(color, 0, 0, size * MathF.Min(0.25f, 0.1f / MathF.Max(0, 1 - MathF.Pow(1 - progress, 4))), 0, MathF.PI * 2);
            var circleRadiusA = size * MathF.Min(0.25f, 0.1f / MathF.Max(0, 1 - MathF.Pow(1 - progress, 4)));
            renderer.DrawTexture(_circleTexture, -circleRadiusA, -circleRadiusA, circleRadiusA * 2, circleRadiusA * 2,
                new TintInfo(new Vector3(color.R / 255f, color.G / 255f, color.B / 255f), 0, alpha));
            
            alpha /= 4;
            color = Color.FromArgb((int)(alpha * 255), 0xff, 0xee, 0xaa);
            // renderer.DrawSectorSmooth(color, 0, 0, size * 0.5f * MathF.Pow(progress, 0.25f), 0, MathF.PI * 2);
            var circleRadiusB = size * 0.5f * MathF.Pow(progress, 0.25f);
            renderer.DrawTexture(_circleTexture, -circleRadiusB, -circleRadiusB, circleRadiusB * 2, circleRadiusB * 2,
                new TintInfo(new Vector3(color.R / 255f, color.G / 255f, color.B / 255f), 0, alpha));
            
            color = Color.FromArgb((int)(MathF.Pow(sThickProg, 0.33f) * 255), 0xff, 0xee, 0xaa);
            
            foreach (var p in Particles)
            {
                var x = p.Position.X * noteRatio;
                var y = p.Position.Y * noteRatio;
                var s = (MathF.Pow(progress, 0.25f) * 7.5f + 7.5f) * noteRatio;
                
                var tx = renderer.Transform.M11 * x + renderer.Transform.M12 * y + renderer.Transform.M14;
                var ty = renderer.Transform.M21 * x + renderer.Transform.M22 * y + renderer.Transform.M24;
                if (tx + s < 0 || tx - s > w || ty + s < 0 || ty - s > h) continue;
                renderer.DrawRect(color, -s + x, -s + y, s * 2, s * 2);
            }
            
            renderer.Transform = t;

            if (progress >= 1)
            {
                NotNeeded = true;
            }
        }
    }
}