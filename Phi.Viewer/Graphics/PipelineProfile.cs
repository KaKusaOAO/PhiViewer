using Veldrid;

namespace Phi.Viewer.Graphics
{
    public class PipelineProfile
    {
        public Framebuffer Framebuffer { get; set; }
        public Pipeline Pipeline { get; set; }
        public GraphicsPipelineDescription PipelineDescription { get; set; }
        
        public void DisposeResources()
        {
            Framebuffer?.Dispose();
            Pipeline?.Dispose();
        }
    }
}