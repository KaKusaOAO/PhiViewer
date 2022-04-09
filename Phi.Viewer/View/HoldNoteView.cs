using System;
using Phi.Charting.Notes;
using Phi.Resources;
using Phi.Viewer.Utils;
using Veldrid;

namespace Phi.Viewer.View
{
    public class HoldNoteView : AbstractNoteView
    {
        internal static Texture _texture;
        internal static Texture _textureHL;
        
        internal static Texture _textureHead;
        internal static Texture _textureHeadHL;
        
        internal static Texture _textureEnd;

        private double _lastJudge = 0;

        static HoldNoteView()
        {
            var asm = ResourceHelper.Assembly;
            var stream = asm.GetManifestResourceStream("Phi.Resources.Textures.Hold.png");
            _texture = ImageLoader.LoadTextureFromStream(stream);
            
            stream = asm.GetManifestResourceStream("Phi.Resources.Textures.Hold2HL_1.png");
            _textureHL = ImageLoader.LoadTextureFromStream(stream);
            
            
            stream = asm.GetManifestResourceStream("Phi.Resources.Textures.Hold_Head.png");
            _textureHead = ImageLoader.LoadTextureFromStream(stream);
            
            stream = asm.GetManifestResourceStream("Phi.Resources.Textures.Hold2HL_0.png");
            _textureHeadHL = ImageLoader.LoadTextureFromStream(stream);
            
            
            stream = asm.GetManifestResourceStream("Phi.Resources.Textures.Hold_End.png");
            _textureEnd = ImageLoader.LoadTextureFromStream(stream);
        }
        
        public HoldNoteView(JudgeLineView parent, Note model)
        {
            Parent = parent;
            Model = model;
        }
        
        private Texture RenderTexture => Model.HasSibling ? _textureHL : _texture;
        
        private Texture HeadRenderTexture => Model.HasSibling ? _textureHeadHL : _textureHead;

        public override bool DoesClipOnPositiveSpeed => true;

        public override float ClearTime => InternalGetClearTime();

        private float InternalGetClearTime()
        {
            var ct = (Model.Time + Model.HoldTime) / (Parent.Model.Bpm / 1875);
            ct -= 250;
            return MathF.Max(Model.Time, Parent.GetConvertedGameTime(ct));
        }

        public override void Render()
        {
            var viewer = PhiViewer.Instance;
            if (IsOffscreen() && viewer.ForceRenderOffscreen) return;

            var gt = Parent.GetConvertedGameTime(viewer.Time);
            if (gt <= Model.Time + Model.HoldTime)
            {
                var renderer = viewer.Renderer;
                var ratio = viewer.NoteRatio;
                var yPos = Parent.GetYPosWithGame(Model.Time);
                var xPos = GetXPos();

                var w = NoteWidth * ratio;
                var h = Parent.GetYPosWithGame(Model.Time + Model.HoldTime) - Parent.GetYPosWithGame(Model.Time);
                
                var headH = (float) _textureHead.Height / _textureHead.Width * w;
                var endH = (float) _textureEnd.Height / _textureEnd.Width * w;
                renderer.DrawTexture(_textureEnd, -w / 2 + xPos, -yPos - h, w, endH);

                if (Model.HasSibling)
                {
                    w *= 1060f / 989f * 1.025f;
                    endH -= ratio * 1060f / 989f;
                }
                
                renderer.DrawTexture(RenderTexture, -w / 2 + xPos, -yPos - h + endH, w, h - endH);
                renderer.DrawTexture(HeadRenderTexture, -w / 2 + xPos, -yPos, w, headH);
            }

            if (IsCrossed && gt < Model.Time + Model.HoldTime)
            {
                var now = viewer.MillisSinceLaunch;
                if(now - _lastJudge > 75) {
                    _lastJudge = now;
                    SpawnJudge();
                }
            } else {
                _lastJudge = 0;
            }
        }
    }
}