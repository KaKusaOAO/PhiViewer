using Phi.Charting.Notes;
using Phi.Resources;
using Phi.Viewer.Graphics;
using Phi.Viewer.Utils;
using Veldrid;

namespace Phi.Viewer.View
{
    public class TapNoteView : AbstractNoteView
    {
        internal static Texture _texture;
        internal static Texture _textureHL;

        static TapNoteView()
        {
            var asm = ResourceHelper.Assembly;
            var stream = asm.GetManifestResourceStream("Phi.Resources.Textures.Tap2.png");
            _texture = ImageLoader.LoadTextureFromStream(stream);

            stream = asm.GetManifestResourceStream("Phi.Resources.Textures.Tap2HL.png");
            _textureHL = ImageLoader.LoadTextureFromStream(stream);
        }
        
        public TapNoteView(JudgeLineView parent, Note model)
        {
            Parent = parent;
            Model = model;
        }
        
        private Texture RenderTexture => Model.HasSibling ? _textureHL : _texture;
        
        public override void Render()
        {
            var viewer = PhiViewer.Instance;
            if (IsOffscreen() && viewer.ForceRenderOffscreen) return;

            var renderer = viewer.Renderer;
            var ratio = viewer.NoteRatio;
            var yPos = GetYPos();
            var xPos = GetXPos();

            var w = NoteWidth * ratio;
            var h = (Model.HasSibling ? 32 : 18) * ratio;

            if (!IsCleared)
            {
                renderer.DrawTexture(RenderTexture, -w / 2 + xPos, -h / 2 - yPos, w, h);
            }
        }
    }
}