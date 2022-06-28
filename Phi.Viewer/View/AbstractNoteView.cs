using System;
using System.IO;
using System.Numerics;
using Phi.Charting.Notes;
using Phi.Resources;
using Phi.Viewer.Graphics;

namespace Phi.Viewer.View
{
    public abstract class AbstractNoteView : IDisposable
    {
        protected static Stream TapFXAudioStream { get; private set; }
        protected static Stream FlickFXAudioStream { get; private set; }
        protected static Stream CatchFXAudioStream { get; private set; }

        static AbstractNoteView()
        {
            var asm = typeof(ResourceHelper).Assembly;
            TapFXAudioStream = asm.GetManifestResourceStream("Phi.Resources.Audio.TapFX.wav");
            FlickFXAudioStream = asm.GetManifestResourceStream("Phi.Resources.Audio.FlickFX.wav");
            CatchFXAudioStream = asm.GetManifestResourceStream("Phi.Resources.Audio.CatchFX.wav");
        }
        
        public const float NoteWidth = 240;
        
        public JudgeLineView Parent { get; protected set; }
        
        public Note Model { get; set; }

        public virtual float ClearTime => Model.Time;
        
        public bool IsCleared { get; private set; }
        
        public bool IsCrossed { get; private set; }
        
        public NoteSide Side { get; private set; }
        
        public bool IsInspectorHighlightedOnNextDraw { get; set; }

        public virtual bool DoesClipOnPositiveSpeed => false;

        public virtual MeshRenderer MeshRenderer => null;

        public void Update()
        {
            var viewer = PhiViewer.Instance;
            var gt = Parent.GetConvertedGameTime(viewer.Time);

            var cleared = gt >= ClearTime;
            var crossed = gt >= Model.Time;
            if (!cleared) IsCleared = false;
            if (!crossed) IsCrossed = false;

            if (cleared && !IsCleared) IsCleared = true;

            if (crossed && !IsCrossed)
            {
                IsCrossed = true;

                if (gt - Model.Time < 10 && viewer.IsPlaying)
                {
                    if (viewer.EnableClickSound)
                    {
                        viewer.AddAnimatedObject(new OneShotAudio(GetClearAudioStream()));
                    }
                    
                    SpawnJudge();
                }
            }
        }

        public float GetXPos()
        {
            var viewer = PhiViewer.Instance;
            var xPos = 0.845f * Model.PosX / 15;
            var rp = Parent.GetRotatedPos(new Vector2(xPos, 0));
            var sp = Parent.GetScaledPos(rp);

            sp.X -= viewer.RenderXPad;
            sp.Y -= viewer.WindowSize.Height;
            sp.Y *= 1.8f * viewer.NoteRatio / viewer.Ratio;

            var fp = Parent.GetRotatedPos(sp, Parent.GetLineRotation(viewer.Time));
            xPos = fp.X;
            
            return xPos;
        }

        public virtual float GetYPos()
        {
            var viewer = PhiViewer.Instance;
            if (viewer.PreferTimeBasedYPos)
                return Parent.GetYPosWithGame(Model.Time) * (viewer.UseUniqueSpeed || this is HoldNoteView ? 1 : Model.Speed);
            return (Model.FloorPosition - Parent.CurrentFloorPosition) * JudgeLineView.FloorPositionYScale;
        }

        public bool IsOffscreen()
        {
            var viewer = PhiViewer.Instance;
            var ns = Model.Time;
            var ne = ns + Model.HoldTime;
            var gt = Parent.GetConvertedGameTime(viewer.Time);
            if (gt >= ns && gt <= ne) return false;

            var r = MathF.Max(viewer.WindowSize.Width, viewer.WindowSize.Height);
            return MathF.Abs(Parent.GetYPosWithGame(Model.Time)) > r
                   && MathF.Abs(Parent.GetYPosWithGame(Model.Time + Model.HoldTime)) > r;
        }
        
        public abstract void Render();

        public static AbstractNoteView FromModel(JudgeLineView line, Note model, NoteSide side)
        {
            AbstractNoteView result = new TapNoteView(line, model);
            
            switch (model.Type)
            {
                case NoteType.Flick:
                    result = new FlickNoteView(line, model);
                    break;
                case NoteType.Hold:
                    result = new HoldNoteView(line, model);
                    break;
                case NoteType.Catch:
                    result = new CatchNoteView(line, model);
                    break;
            }

            result.Side = side;
            return result;
        }

        public void SpawnJudge()
        {
            var viewer = PhiViewer.Instance;
            if (!viewer.EnableParticles) return;

            var linePos = Parent.GetScaledPos(Parent.GetLinePos(viewer.Time));
            var rad = -Parent.GetLineRotation(viewer.Time) / 180 * MathF.PI;
            var xPos = GetXPos();

            var px = MathF.Cos(rad) * xPos + linePos.X;
            var py = MathF.Sin(rad) * xPos + linePos.Y;
            var j = new JudgeEffect(px, py, 4);
            viewer.AddAnimatedObject(j);
        }

        public virtual Stream GetClearAudioStream() => TapFXAudioStream;

        public void Dispose()
        {
            
        }
    }
}