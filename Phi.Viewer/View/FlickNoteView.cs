using System.IO;
using Phi.Charting.Notes;
using Phi.Resources;
using Phi.Viewer.Utils;
using Veldrid;

namespace Phi.Viewer.View
{
    public class FlickNoteView : AbstractNoteView
    {
        internal static Texture _texture;
        internal static Texture _textureHL;

        static FlickNoteView()
        {
            var asm = ResourceHelper.Assembly;
            var stream = asm.GetManifestResourceStream("Phi.Resources.Textures.Flick2.png");
            _texture = ImageLoader.LoadTextureFromStream(stream);
            
            stream = asm.GetManifestResourceStream("Phi.Resources.Textures.Flick2HL.png");
            _textureHL = ImageLoader.LoadTextureFromStream(stream);
        }

        public FlickNoteView(JudgeLineView parent, Note model)
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
            var h = (Model.HasSibling ? 55 : 35) * ratio;

            if (!IsCleared)
            {
                renderer.DrawTexture(RenderTexture, -w / 2 + xPos, -h / 2 - yPos, w, h);
            }
        }

        public override Stream GetClearAudioStream() => FlickFXAudioStream;
    }
}