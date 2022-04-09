using System;
using System.Collections.Generic;
using System.Drawing;
using System.Numerics;

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

            var progress = (float) (viewer.MillisSinceLaunch - StartTime) / (500 / viewer.MusicPlayer.PlaybackRate);
            var t = renderer.Transform;

            var size = 100 * viewer.NoteRatio;
            var alpha = MathF.Pow(MathF.Max(0, 1 - progress), 1.1f);
            var lineWidth = 4 * viewer.NoteRatio;
            var color = Color.FromArgb((int)(alpha * 255), 0xff, 0xee, 0xaa);
            
            renderer.Translate(X * viewer.WindowSize.Width / viewer.RefScreenSize.Width, Y * viewer.WindowSize.Height / viewer.RefScreenSize.Height);

            var s2 = size * (0.75f + 0.25f * (1 - MathF.Pow(1 - progress, 5)));
            renderer.StrokeRect(color, -s2, -s2, s2 * 2, s2 * 2, lineWidth);
            renderer.Rotate(MathF.PI / 4);

            var sThickProg = MathF.Pow(MathF.Max(0, 1 - progress), 2);
            var sThick = 48 * sThickProg;
            lineWidth = sThick * sThickProg * viewer.NoteRatio;
            var s3 = size - sThick * 0.5f * sThickProg * viewer.NoteRatio;
            s3 *= 0.8f + 0.3f * MathF.Pow(progress, 0.25f);
            alpha *= 0.125f;
            color = Color.FromArgb((int)(alpha * 255), 0xff, 0xee, 0xaa);
            renderer.StrokeRect(color, -s3, -s3, s3 * 2, s3 * 2, lineWidth);
            renderer.Rotate(-MathF.PI / 4);

            lineWidth = 6 * viewer.NoteRatio;
            alpha = MathF.Pow(sThickProg, 0.33f);
            color = Color.FromArgb((int)(alpha * 255), 0xff, 0xee, 0xaa);

            var offset = (1 - MathF.Pow(1 - progress, 3)) * (MathF.PI / 4) - MathF.PI / 4;
            renderer.StrokeArc(color, 0, 0, s2 * 0.9f, offset, offset - MathF.PI / 2, lineWidth, true);
            renderer.StrokeArc(color, 0, 0, s2 * 0.9f, offset + MathF.PI / 2, offset + MathF.PI, lineWidth);

            alpha = sThickProg;
            color = Color.FromArgb((int)(alpha * 255), 0xff, 0xee, 0xaa);
            renderer.DrawSectorSmooth(color, 0, 0, size * MathF.Min(0.25f, 0.1f / MathF.Max(0, 1 - MathF.Pow(1 - progress, 4))), 0, MathF.PI * 2);
            
            alpha /= 4;
            color = Color.FromArgb((int)(alpha * 255), 0xff, 0xee, 0xaa);
            renderer.DrawSectorSmooth(color, 0, 0, size * 0.5f * MathF.Pow(progress, 0.25f), 0, MathF.PI * 2);
            
            alpha *= 4;
            color = Color.FromArgb((int)(alpha * 255), 0xff, 0xee, 0xaa);
            foreach (var p in Particles)
            {
                var x = p.Position.X * viewer.NoteRatio;
                var y = p.Position.Y * viewer.NoteRatio;
                var s = (MathF.Pow(progress, 0.25f) * 7.5f + 7.5f) * viewer.NoteRatio;
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