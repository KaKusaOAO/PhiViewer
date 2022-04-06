using System;
using System.Numerics;
using Phi.Charting.Notes;
using Veldrid;

namespace Phi.Viewer.View
{
    public abstract class AbstractNoteView
    {
        public const float NoteWidth = 240;
        
        public JudgeLineView Parent { get; protected set; }
        
        public Note Model { get; set; }

        public virtual float ClearTime => Model.Time;
        
        public bool IsCleared { get; private set; }
        
        public bool IsCrossed { get; private set; }

        public virtual bool DoesClipOnPositiveSpeed => false; 

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
                
                // TODO: Play sound
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

        public static AbstractNoteView FromModel(JudgeLineView line, Note model)
        {
            switch (model.Type)
            {
                case NoteType.Tap:
                    return new TapNoteView(line, model);
                case NoteType.Flick:
                    return new FlickNoteView(line, model);
                case NoteType.Hold:
                    return new HoldNoteView(line, model);
                case NoteType.Catch:
                    return new CatchNoteView(line, model);
            }

            return new TapNoteView(line, model);
        }

        public void SpawnJudge()
        {
            
        }
    }
}