using System;

namespace Phi.Viewer
{
    public abstract class ViewerHost : IDisposable
    {
        public IWindow Window { get; protected set; }

        public virtual void Run(PhiViewer viewer)
        {
            viewer.Host = this;
            viewer.Init();
        }

        public virtual void Dispose()
        {
        }
    }
}