using System;
using Veldrid;

namespace Phi.Viewer
{
    public interface IWindow
    {
        public GraphicsDevice GraphicsDevice { get; }
        
        public bool Exists { get; }
        
        public float Width { get; }
        
        public float Height { get; }

        public event Action WindowResized;

        public InputSnapshot PumpEvents();
    }
}