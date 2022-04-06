using System;
using System.Collections.Generic;
using System.Drawing;
using System.Numerics;
using System.Resources;
using System.Text;
using Phi.Resources;
using SharpDX;
using Veldrid;
using Veldrid.SPIRV;
using Veldrid.Utilities;
using ResourceSet = Veldrid.ResourceSet;

namespace Phi.Viewer
{
    public class Renderer
    {
        public PhiViewer Viewer { get; }
        
        public GraphicsDevice GraphicsDevice => Viewer.Host.Window.GraphicsDevice;
        
        public CommandList CommandList { get; private set; }

        private Matrix4x4 _transform;

        private GraphicsPipelineDescription _pipelineDescription;
        private GraphicsPipelineDescription _clipPD;
        private GraphicsPipelineDescription _mainPD;
        private Pipeline _pipeline;
        
        private DeviceBuffer _indexBuffer;
        
        private Shader[] _shaders;
        private Shader[] _shadersClip;
        private ResourceLayout _fragLayout;
        private ResourceLayout _fragLayoutClip;
        private ResourceLayout _vertLayout;
        
        // Clip framebuffer
        private Stack<Texture> _clipStack = new Stack<Texture>();
        // private Texture _clipColor;
        private Texture _clipDepth;
        private Framebuffer _clipFramebuffer;
        private Pipeline _clipPipeline;

        private Texture _renderColor;
        private Texture _renderDepth;
        private Framebuffer _renderTarget;
        private Pipeline _mainPipeline;

        internal Texture RenderTargetTexture => _renderColor;
        internal Texture RenderTargetDepth => _renderDepth;
        internal Texture ClipBufferTexture => _renderDepth;
        internal Texture ClipBufferDepth => _renderDepth;

        private bool _isClipping;
        
        public Renderer(PhiViewer viewer)
        {
            Viewer = viewer;
            Init();
        }

        private void Init()
        {
            CreateResources();
        }

        private void CreateResources()
        {
            var factory = GraphicsDevice.ResourceFactory;

            ushort[] quadIndices = { 0, 1, 2, 3 };
            BufferDescription ibDescription = new BufferDescription(
                4 * sizeof(ushort),
                BufferUsage.IndexBuffer);
            _indexBuffer = factory.CreateBuffer(ibDescription);
            GraphicsDevice.UpdateBuffer(_indexBuffer, 0, quadIndices);

            VertexLayoutDescription vertexLayout = new VertexLayoutDescription(
                new VertexElementDescription("Position", VertexElementSemantic.TextureCoordinate, VertexElementFormat.Float2),
                new VertexElementDescription("TextureCoordinates", VertexElementSemantic.TextureCoordinate, VertexElementFormat.Float2));

            var asm = ResourceHelper.Assembly;
            var fsh = asm.GetManifestResourceStream("Phi.Resources.Shaders.Textured.fsh");
            var fshClip = asm.GetManifestResourceStream("Phi.Resources.Shaders.ClipMap.fsh");
            var vsh = asm.GetManifestResourceStream("Phi.Resources.Shaders.Basic.vsh");

            var fshBuffer = new byte[fsh.Length];
            fsh.Read(fshBuffer, 0, fshBuffer.Length);
            var fshClipBuffer = new byte[fshClip.Length];
            fshClip.Read(fshClipBuffer, 0, fshClipBuffer.Length);
            var vshBuffer = new byte[vsh.Length];
            vsh.Read(vshBuffer, 0, vshBuffer.Length);
            
            var vertexShaderDesc = new ShaderDescription(ShaderStages.Vertex, vshBuffer, "main");
            var fragmentShaderDesc = new ShaderDescription(ShaderStages.Fragment, fshBuffer, "main");
            var fragmentClipShaderDesc = new ShaderDescription(ShaderStages.Fragment, fshClipBuffer, "main");

            _shaders = factory.CreateFromSpirv(vertexShaderDesc, fragmentShaderDesc);
            _shadersClip = factory.CreateFromSpirv(vertexShaderDesc, fragmentClipShaderDesc);

            _vertLayout = factory.CreateResourceLayout(new ResourceLayoutDescription(
                new ResourceLayoutElementDescription("Transform", ResourceKind.UniformBuffer, ShaderStages.Vertex)));
            
            _fragLayout = factory.CreateResourceLayout(new ResourceLayoutDescription(
                new ResourceLayoutElementDescription("Input", ResourceKind.TextureReadOnly, ShaderStages.Fragment),
                new ResourceLayoutElementDescription("Sampler", ResourceKind.Sampler, ShaderStages.Fragment),
                new ResourceLayoutElementDescription("Tint", ResourceKind.UniformBuffer, ShaderStages.Fragment),
                new ResourceLayoutElementDescription("ClipMap", ResourceKind.TextureReadOnly, ShaderStages.Fragment),
                new ResourceLayoutElementDescription("ClipMapSampler", ResourceKind.Sampler, ShaderStages.Fragment)
                ));
            
            _fragLayoutClip = factory.CreateResourceLayout(new ResourceLayoutDescription
            {
                Elements = Array.Empty<ResourceLayoutElementDescription>()
            });

            // Create pipeline
            _pipelineDescription = new GraphicsPipelineDescription();
            _pipelineDescription.BlendState = BlendStateDescription.SingleAlphaBlend;
            _pipelineDescription.DepthStencilState = new DepthStencilStateDescription(false, false, ComparisonKind.Always,
                false, 
                new StencilBehaviorDescription(StencilOperation.Keep, StencilOperation.Keep, StencilOperation.Keep, ComparisonKind.Always),
                new StencilBehaviorDescription(StencilOperation.Keep, StencilOperation.Keep, StencilOperation.Keep, ComparisonKind.Always),
                0, 0, 0);
            _pipelineDescription.RasterizerState = new RasterizerStateDescription(FaceCullMode.None, PolygonFillMode.Solid, 
                FrontFace.Clockwise, false, false);
            _pipelineDescription.PrimitiveTopology = PrimitiveTopology.TriangleStrip;
            _pipelineDescription.ResourceLayouts = new [] { _vertLayout, _fragLayout };
            _pipelineDescription.ShaderSet = new ShaderSetDescription(
                vertexLayouts: new[] { vertexLayout },
                shaders: _shaders);
            
            // Main swapchain
            _mainPD = new GraphicsPipelineDescription();
            _mainPD.BlendState = BlendStateDescription.SingleAlphaBlend;
            _mainPD.DepthStencilState = new DepthStencilStateDescription(false, false, ComparisonKind.Always,
                false, 
                new StencilBehaviorDescription(StencilOperation.Keep, StencilOperation.Keep, StencilOperation.Keep, ComparisonKind.Always),
                new StencilBehaviorDescription(StencilOperation.Keep, StencilOperation.Keep, StencilOperation.Keep, ComparisonKind.Always),
                0, 0, 0);
            _mainPD.RasterizerState = new RasterizerStateDescription(FaceCullMode.None, PolygonFillMode.Solid, FrontFace.Clockwise, false, false);
            _mainPD.PrimitiveTopology = PrimitiveTopology.TriangleStrip;
            _mainPD.ResourceLayouts = new [] { _vertLayout, _fragLayout };
            _mainPD.ShaderSet = new ShaderSetDescription(
                vertexLayouts: new[] { vertexLayout },
                shaders: _shaders);
            _mainPD.Outputs = GraphicsDevice.MainSwapchain.Framebuffer.OutputDescription;
            _mainPipeline = factory.CreateGraphicsPipeline(_mainPD);

            // Clip
            _clipPD = new GraphicsPipelineDescription();
            _clipPD.BlendState = BlendStateDescription.SingleAlphaBlend;
            _clipPD.DepthStencilState = new DepthStencilStateDescription(false, false, ComparisonKind.Never,
                false, 
                new StencilBehaviorDescription(StencilOperation.Keep, StencilOperation.Keep, StencilOperation.Keep, ComparisonKind.Never),
                new StencilBehaviorDescription(StencilOperation.Keep, StencilOperation.Keep, StencilOperation.Keep, ComparisonKind.Never),
                0, 0, 0);
            _clipPD.RasterizerState = new RasterizerStateDescription(FaceCullMode.None, PolygonFillMode.Solid, FrontFace.Clockwise, false, false);
            _clipPD.PrimitiveTopology = PrimitiveTopology.TriangleStrip;
            _clipPD.ResourceLayouts = new [] { _vertLayout, _fragLayoutClip };
            _clipPD.ShaderSet = new ShaderSetDescription(
                vertexLayouts: new[] { vertexLayout },
                shaders: _shadersClip);

            CommandList = factory.CreateCommandList();
            UpdateRenderTarget();
        }

        private void UpdateRenderTarget()
        {
            var factory = GraphicsDevice.ResourceFactory;
            var window = Viewer.Host.Window;
            
            _renderColor?.Dispose();
            _renderDepth?.Dispose();
            _renderTarget?.Dispose();
            _pipeline?.Dispose();
            foreach (var texture in _clipStack)
            {
                texture.Dispose();
            }
            _clipStack.Clear();
            _clipDepth?.Dispose();
            _clipFramebuffer?.Dispose();
            _clipPipeline?.Dispose();
            _mainPipeline?.Dispose();
            
            _renderColor = factory.CreateTexture(TextureDescription.Texture2D((uint) window.Width, (uint) window.Height, 1, 1,
                PixelFormat.R8_G8_B8_A8_UNorm, TextureUsage.RenderTarget | TextureUsage.Sampled));
            _renderDepth = factory.CreateTexture(TextureDescription.Texture2D((uint) window.Width, (uint) window.Height, 1, 1, PixelFormat.R16_UNorm,
                TextureUsage.DepthStencil | TextureUsage.Sampled));
            _renderTarget = factory.CreateFramebuffer(new FramebufferDescription(_renderDepth, _renderColor));
            _pipelineDescription.Outputs = _renderTarget.OutputDescription;
            _pipeline = factory.CreateGraphicsPipeline(_pipelineDescription);

            var clipColor = factory.CreateTexture(TextureDescription.Texture2D((uint) window.Width, (uint) window.Height, 1, 1,
                PixelFormat.R8_G8_B8_A8_UNorm, TextureUsage.RenderTarget | TextureUsage.Sampled | TextureUsage.Storage));
            _clipDepth = factory.CreateTexture(TextureDescription.Texture2D((uint) window.Width, (uint) window.Height, 1, 1, PixelFormat.R16_UNorm,
                TextureUsage.DepthStencil | TextureUsage.Sampled | TextureUsage.Storage));
            _clipStack.Push(clipColor);
            
            _clipFramebuffer = factory.CreateFramebuffer(new FramebufferDescription(_clipDepth, clipColor));
            _clipPD.Outputs = _clipFramebuffer.OutputDescription;
            _clipPipeline = factory.CreateGraphicsPipeline(_clipPD);
            
            _mainPD.Outputs = GraphicsDevice.SwapchainFramebuffer.OutputDescription;
            _mainPipeline = factory.CreateGraphicsPipeline(_mainPD);
        }

        public void Begin()
        {
            var window = Viewer.Host.Window;
            GraphicsDevice.MainSwapchain.Resize((uint) window.Width, (uint) window.Height);
            
            CommandList.Begin();
            ClearClip();
            
            CommandList.SetFramebuffer(GraphicsDevice.SwapchainFramebuffer);
            CommandList.ClearColorTarget(0, RgbaFloat.Black);
            
            CommandList.SetFramebuffer(_renderTarget);
            CommandList.SetPipeline(_pipeline);
            CommandList.ClearColorTarget(0, RgbaFloat.Black);
            CommandList.SetIndexBuffer(_indexBuffer, IndexFormat.UInt16);
            CommandList.SetViewport(0, new Viewport(0, 0, window.Width, window.Height, 0, 1));
        }
        public void DrawQuad(float x, float y, float width, float height)
        {
            Vector4[] quadVerts =
            {
                new Vector4(x, y, 0, 0),
                new Vector4(x + width, y, 1, 0),
                new Vector4(x, y + height, 0, 1),
                new Vector4(x + width, y + height, 1, 1),
            };
            BufferDescription vbDescription = new BufferDescription(64, BufferUsage.VertexBuffer);
            
            using var vertexBuffer = GraphicsDevice.ResourceFactory.CreateBuffer(vbDescription);
            GraphicsDevice.UpdateBuffer(vertexBuffer, 0, quadVerts);
            CommandList.SetVertexBuffer(0, vertexBuffer);
            
            var factory = new DisposeCollectorResourceFactory(GraphicsDevice.ResourceFactory);
            using var matrixInfoBuffer = factory.CreateBuffer(new BufferDescription(MatrixInfo.SizeInBytes, BufferUsage.UniformBuffer));
            GraphicsDevice.UpdateBuffer(
                matrixInfoBuffer, 0, new MatrixInfo
                {
                    Transform = _transform,
                    Resolution = new Vector2(Viewer.WindowSize.Width, Viewer.WindowSize.Height)
                });
            
            using var resourceSet = factory.CreateResourceSet(new ResourceSetDescription(
                _vertLayout, matrixInfoBuffer));
            CommandList.SetGraphicsResourceSet(0, resourceSet);
            CommandList.DrawIndexed(4);
        }

        public void DrawTexture(Texture texture, float x, float y, float width, float height, TintInfo? tintInfo = null)
        {
            var factory = new DisposeCollectorResourceFactory(GraphicsDevice.ResourceFactory);
            using var view = factory.CreateTextureView(texture);
            DrawTexture(view, x, y, width, height, tintInfo);
        }

        public void DrawToMainSwapchain()
        {
            CommandList.SetFramebuffer(GraphicsDevice.SwapchainFramebuffer);
            CommandList.SetPipeline(_mainPipeline);
            CommandList.ClearColorTarget(0, RgbaFloat.Black);
            DrawTexture(_renderColor, 0, 0, Viewer.WindowSize.Width, Viewer.WindowSize.Height);
        }
        
        public void DrawTexture(TextureView view, float x, float y, float width, float height, TintInfo? tintInfo = null)
        {
            var factory = new DisposeCollectorResourceFactory(GraphicsDevice.ResourceFactory);
            using var tintInfoBuffer = factory.CreateBuffer(new BufferDescription(32, BufferUsage.UniformBuffer));
            GraphicsDevice.UpdateBuffer(
                tintInfoBuffer, 0,
                tintInfo ?? new TintInfo(new Vector3(1f, 1f, 1f), 0f, 1f));

            using var resourceSet = factory.CreateResourceSet(new ResourceSetDescription(
                _fragLayout, view, GraphicsDevice.PointSampler, tintInfoBuffer, _clipStack.Peek(), GraphicsDevice.PointSampler));
            CommandList.SetGraphicsResourceSet(1, resourceSet);
            
            DrawQuad(x, y, width, height);
        }
        
        public void DrawRect(Color color, float x, float y, float width, float height)
        {
            var factory = new DisposeCollectorResourceFactory(GraphicsDevice.ResourceFactory);
            using var tintInfoBuffer = factory.CreateBuffer(new BufferDescription(32, BufferUsage.UniformBuffer));
            GraphicsDevice.UpdateBuffer(
                tintInfoBuffer, 0,
                new TintInfo(new Vector3(color.R / 255f, color.G / 255f, color.B / 255f), 1, color.A / 255f));

            using var tex = factory.CreateTexture(new TextureDescription(1, 1, 1, 1, 1, PixelFormat.R8_G8_B8_A8_UNorm,
                TextureUsage.Sampled, TextureType.Texture2D));
            using var view = factory.CreateTextureView(tex);

            ResourceSet resourceSet;
            if (!_isClipping)
            {
                resourceSet = factory.CreateResourceSet(new ResourceSetDescription(
                    _fragLayout, view, GraphicsDevice.PointSampler, tintInfoBuffer, _clipStack.Peek(),
                    GraphicsDevice.PointSampler));
                CommandList.SetGraphicsResourceSet(1, resourceSet);
            }
            else
            {
                resourceSet = factory.CreateResourceSet(new ResourceSetDescription(_fragLayoutClip));
                CommandList.SetGraphicsResourceSet(1, resourceSet);
            }
            
            DrawQuad(x, y, width, height);
            resourceSet.Dispose();
        }
        public void Render()
        {
            CommandList.End();
            GraphicsDevice.SubmitCommands(CommandList);
            GraphicsDevice.SwapBuffers();
            
            UpdateRenderTarget();
            GC.Collect();
        }

        public Matrix4x4 Transform
        {
            get => _transform;
            set => _transform = value;
        }

        public void Translate(float x, float y, float z = 0)
        {
            Matrix4x4 mult = Matrix4x4.Identity;
            mult.M14 += x;
            mult.M24 += y;
            mult.M34 += z;
            _transform *= mult;
        }

        public void Rotate(float radians)
        {
            Matrix4x4 mult = Matrix4x4.Identity;
            mult.M11 = MathF.Cos(radians);
            mult.M12 = -MathF.Sin(radians);
            mult.M21 = MathF.Sin(radians);
            mult.M22= MathF.Cos(radians);
            _transform *= mult;
        }

        public void Scale(float x, float y)
        {
            Matrix4x4 mult = Matrix4x4.Identity;
            mult.M11 *= x;
            mult.M22 *= y;
            _transform *= mult;
        }

        public void EnableClip()
        {
            CommandList.SetFramebuffer(_clipFramebuffer);
            CommandList.ClearColorTarget(0, RgbaFloat.Black);
            CommandList.SetFramebuffer(_renderTarget);
        }

        public void PushClip()
        {
            var factory = GraphicsDevice.ResourceFactory;
            
            var old = _clipStack.Peek();
            var clipColor = factory.CreateTexture(TextureDescription.Texture2D(old.Width, old.Height, 1, 1,
                PixelFormat.R8_G8_B8_A8_UNorm, TextureUsage.RenderTarget | TextureUsage.Sampled | TextureUsage.Storage));
            CommandList.CopyTexture(old, clipColor);
            _clipStack.Push(clipColor);
            
            UpdateClipFramebufferTarget();
        }

        public void PopClip()
        {
            var tex = _clipStack.Pop();
            tex.Dispose();
            UpdateClipFramebufferTarget();
        }

        private void UpdateClipFramebufferTarget()
        {
            var factory = GraphicsDevice.ResourceFactory;
            var clipColor = _clipStack.Peek();
            
            _clipPipeline?.Dispose();
            _clipFramebuffer = factory.CreateFramebuffer(new FramebufferDescription(_clipDepth, clipColor));
            _clipPD.Outputs = _clipFramebuffer.OutputDescription;
            _clipPipeline = factory.CreateGraphicsPipeline(_clipPD);
        }

        public void ClipRect(float x, float y, float width, float height)
        {
            _isClipping = true;
            CommandList.SetFramebuffer(_clipFramebuffer);
            CommandList.SetPipeline(_clipPipeline);
            DrawRect(Color.White, x, y, width, height);
            
            _isClipping = false;
            CommandList.SetFramebuffer(_renderTarget);
            CommandList.SetPipeline(_pipeline);
        }
        
        public void ClearClip()
        {
            CommandList.SetFramebuffer(_clipFramebuffer);
            CommandList.ClearColorTarget(0, RgbaFloat.White);
            CommandList.SetFramebuffer(_renderTarget);
        }
    }
    
    public struct TintInfo
    {
        public Vector3 RGBTintColor;
        public float TintFactor;
        public float FinalAlpha;
        public TintInfo(Vector3 rGBTintColor, float tintFactor, float finalAlpha)
        {
            RGBTintColor = rGBTintColor;
            TintFactor = tintFactor;
            FinalAlpha = finalAlpha;
        }
    }

    public struct MatrixInfo
    {
        public Matrix4x4 Transform;
        public Vector2 Resolution;

        public const int SizeInBytes = 16 * sizeof(float) + 4 * sizeof(float);

        public MatrixInfo(Matrix4x4 transform, Vector2 resolution)
        {
            Transform = transform;
            Resolution = resolution;
        }
    }
}