using System;
using System.Collections.Generic;
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

            var rand = new Random();
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
                p.Motion *= MathF.Pow(0.92f, MathF.Pow(viewer.PlaybackRate, 0.275f));
            }
        }

        public override void Update()
        {
            base.Update();
            var viewer = PhiViewer.Instance;
            var renderer = viewer.Renderer;

            var progress = (viewer.MillisSinceLaunch - StartTime) / (500 / viewer.PlaybackRate);
            var t = renderer.Transform;

            var size = 100 * viewer.NoteRatio;
            
            renderer.Translate(X * viewer.WindowSize.Width / viewer.RefScreenSize.Width, Y * viewer.WindowSize.Height / viewer.RefScreenSize.Height);
            
        }
    }
}