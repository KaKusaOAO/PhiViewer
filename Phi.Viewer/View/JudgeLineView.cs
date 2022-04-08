using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Numerics;
using Phi.Charting;
using Phi.Charting.Events;
using Phi.Viewer.Utils;

namespace Phi.Viewer.View
{
    public class JudgeLineView
    {
        private struct MeterEntry
        {
            public float Time;
            public float EndTime;
            public float StartY;
            public float EndY;
            public float Speed;
        }
        
        public JudgeLine Model { get; }

        public List<AbstractNoteView> NotesAbove { get; }
        
        public List<AbstractNoteView> NotesBelow { get; }

        private List<MeterEntry> _meter = new List<MeterEntry>();
        
        public JudgeLineView(JudgeLine model)
        {
            Model = model;
            NotesAbove = model.NotesAbove.Select(n => AbstractNoteView.FromModel(this, n, NoteSide.Above)).ToList();
            NotesBelow = model.NotesBelow.Select(n => AbstractNoteView.FromModel(this, n, NoteSide.Below)).ToList();
        }

        public float GetConvertedGameTime(float time) => time * Model.Bpm / 1875;
        
        public float GetRealTimeFromEventTime(float time) => time / Model.Bpm * 1875;

        public Vector2 GetLinePos(float time)
        {
            time = GetConvertedGameTime(time);
            var ev = Model.LineMoveEvents.Find(e => time > e.StartTime && time <= e.EndTime) ?? Model.LineMoveEvents[0];
            if (ev == null) return new Vector2(0.5f, 0.5f);

            var progress = (time - ev.StartTime) / (ev.EndTime - ev.StartTime);
            return new Vector2(
                M.Lerp(ev.Start, ev.End, progress),
                M.Lerp(ev.Start2, ev.End2, progress)
            );
        }

        public float GetLineRotation(float time)
        {
            time = GetConvertedGameTime(time);
            var ev = Model.LineRotateEvents.Find(e => time > e.StartTime && time <= e.EndTime) ?? Model.LineRotateEvents[0];
            if (ev == null) return 0;

            var progress = (time - ev.StartTime) / (ev.EndTime - ev.StartTime);
            return M.Lerp(ev.Start, ev.End, progress);
        }
        
        public float GetLineAlpha(float time)
        {
            time = GetConvertedGameTime(time);
            var ev = Model.LineFadeEvents.Find(e => time > e.StartTime && time <= e.EndTime) ?? Model.LineFadeEvents[0];
            if (ev == null) return 0;

            var progress = (time - ev.StartTime) / (ev.EndTime - ev.StartTime);
            return M.Lerp(ev.Start, ev.End, progress);
        }

        public float GetSpeed(float time)
        {
            time = GetConvertedGameTime(time);
            var ev = Model.SpeedEvents.Find(e => time > e.StartTime && time <= e.EndTime) ?? Model.SpeedEvents[0];
            if (ev == null) return 1;

            var progress = (time - ev.StartTime) / (ev.EndTime - ev.StartTime);
            return ev.Value;
        }

        public void ClearMeter()
        {
            _meter.Clear();
        }

        public float GetYPos(float time)
        {
            var viewer = PhiViewer.Instance;
            var multiplier = 0.00001f * viewer.RefScreenSize.Height * viewer.Host.Window.Height /
                             MathF.Pow(Model.Bpm / 127, 1.5f);
            if (viewer.UseUniqueSpeed) return multiplier * time;

            if (!_meter.Any())
            {
                var meter = 0f;
                var ev = Model.SpeedEvents[0] ?? new SpeedEvent { Value = 1 };

                var i = 0;

                while (ev != null)
                {
                    var lastStartMark = ev.StartTime;
                    var lastSpeed = ev.Value;

                    var m = meter;
                    meter += (ev.EndTime - lastStartMark) * lastSpeed;
                    _meter.Add(new MeterEntry
                    {
                        Time = ev.StartTime,
                        EndTime = ev.EndTime,
                        StartY = m,
                        EndY = meter,
                        Speed = lastSpeed
                    });
                    ev = ++i < Model.SpeedEvents.Count ? Model.SpeedEvents[i] : null;
                }
            }

            var i2 = 0;
            var meter2 = _meter[0];
            var y = 0f;
            while (meter2.Time < time)
            {
                y = meter2.StartY + meter2.Speed * (time - meter2.Time);
                if (++i2 < _meter.Count)
                {
                    meter2 = _meter[i2];
                }
                else
                {
                    break;
                }
            }

            return y * multiplier;
        }

        public float GetYPosWithGame(float time)
        {
            var t = PhiViewer.Instance.Time;
            var gt = GetConvertedGameTime(t);
            return GetYPos(time) - GetYPos(gt);
        }

        public Vector2 GetScaledPos(Vector2 pos)
        {
            var viewer = PhiViewer.Instance;
            var x = pos.X;
            var y = pos.Y;
            var pad = viewer.RenderXPad;
            var cw = viewer.WindowSize.Width - pad * 2;
            var ch = viewer.WindowSize.Height;

            x = 0.5f * cw + (x - 0.5f) * cw + pad;
            y = ch - 0.5f * ch - (y - 0.5f) * ch;
            return new Vector2(x, y);
        }

        public Vector2 GetRotatedPos(Vector2? pos = null, float? angle = null)
        {
            var viewer = PhiViewer.Instance;
            pos ??= GetLinePos(viewer.Time);
            angle ??= GetLineRotation(viewer.Time);

            var rad = angle.Value / 180 * MathF.PI;
            var c = MathF.Cos(rad);
            var s = MathF.Sin(rad);

            var x = c * pos.Value.X - s * pos.Value.Y;
            var y = s * pos.Value.X + c * pos.Value.Y;
            return new Vector2(x, y);
        }

        public void RenderNotes(Predicate<AbstractNoteView> predicate)
        {
            var viewer = PhiViewer.Instance;
            var renderer = viewer.Renderer;
            var cw = viewer.WindowSize.Width - viewer.RenderXPad * 2;
            var ch = viewer.WindowSize.Height;
            
            var t = renderer.Transform;
            var time = viewer.Time; 

            var linePos = GetScaledPos(GetLinePos(time));
            var lineRot = -GetLineRotation(time) / 180 * MathF.PI;
            
            renderer.Translate(linePos.X, linePos.Y);
            renderer.Rotate(lineRot);

            void RenderChildNote(AbstractNoteView n)
            {
                n.Update();
                if (n.IsOffscreen() && !viewer.ForceRenderOffscreen) return;

                var speed = GetSpeed(time);
                var doClip = (speed < 0 || n.DoesClipOnPositiveSpeed) &&
                             (!n.IsOffscreen() || viewer.ForceRenderOffscreen);

                renderer.CommandList.PushDebugGroup("RenderChildNote");
                if (doClip)
                {
                    renderer.PushClip();
                    renderer.ClipRect(-cw, -ch * 2, cw * 2, ch * 2);
                }

                var profile = renderer.CurrentProfile;
                renderer.CurrentProfile = n.IsInspectorHighlightedOnNextDraw ? renderer.BasicAdditive : renderer.BasicNormal;
                n.Render();
                renderer.CurrentProfile = profile;
                n.IsInspectorHighlightedOnNextDraw = false;

                if (doClip)
                {
                     renderer.PopClip();
                }
                renderer.CommandList.PopDebugGroup();
            }

            foreach (var n in NotesAbove.Where(n => predicate(n))) RenderChildNote(n);
            renderer.Scale(1, -1);
            foreach (var n in NotesBelow.Where(n => predicate(n))) RenderChildNote(n);

            renderer.Transform = t;
        }

        public void RenderLine()
        {
            var viewer = PhiViewer.Instance;
            var renderer = viewer.Renderer;
            
            var t = renderer.Transform;
            var time = viewer.Time; 

            var linePos = GetScaledPos(GetLinePos(time));
            var lineRot = -GetLineRotation(time) / 180 * MathF.PI;
            
            renderer.Translate(linePos.X, linePos.Y);
            renderer.Rotate(lineRot);

            var cw = viewer.WindowSize.Width - viewer.RenderXPad * 2;
            var thickness = 8 * viewer.NoteRatio;
            renderer.DrawRect(Color.FromArgb((int)(GetLineAlpha(time) * 255), Color.White), -cw * 2, thickness / -2, cw * 4, thickness);
            
            renderer.Transform = t;
        }
    }
}