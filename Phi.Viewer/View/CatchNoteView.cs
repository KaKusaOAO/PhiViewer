﻿using Phi.Charting.Notes;
using Phi.Resources;
using Phi.Viewer.Utils;
using Veldrid;
using Veldrid.Utilities;

namespace Phi.Viewer.View
{
    public class CatchNoteView : AbstractNoteView
    {
        internal static Texture _texture;
        internal static Texture _textureHL;

        static CatchNoteView()
        {
            var asm = ResourceHelper.Assembly;
            var stream = asm.GetManifestResourceStream("Phi.Resources.Textures.Drag.png");
            _texture = ImageLoader.LoadTextureFromStream(stream);

            stream = asm.GetManifestResourceStream("Phi.Resources.Textures.DragHL.png");
            _textureHL = ImageLoader.LoadTextureFromStream(stream);
        }
        
        public CatchNoteView(JudgeLineView parent, Note model)
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
            var yPos = Parent.GetYPosWithGame(Model.Time) * (viewer.UseUniqueSpeed ? 1 : Model.Speed);
            var xPos = GetXPos();

            var w = NoteWidth * ratio;
            var h = (Model.HasSibling ? 28 : 12) * ratio;

            if (!IsCleared)
            {
                renderer.DrawTexture(RenderTexture, -w / 2 + xPos, -h / 2 - yPos, w, h);
            }
        }
    }
}