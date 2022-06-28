using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Numerics;
using System.Runtime.InteropServices;
using FreeTypeSharp;
using FreeTypeSharp.Native;
using Phi.Resources;
using Veldrid;
using Veldrid.SPIRV;
using Veldrid.Utilities;
using static FreeTypeSharp.Native.FT;
using Rectangle = System.Drawing.Rectangle;

namespace Phi.Viewer.Graphics
{
    public class Renderer
    {
        private struct ClipStateEntry : IDisposable
        {
            public Texture Texture;
            public Texture Depth;
            public PipelineProfile Profile;

            public void Dispose()
            {
                Texture?.Dispose();
                Depth?.Dispose();
                Profile?.DisposeResources();
            }
        }
        
        private struct DrawableCharacter
        {
            public Texture Texture;
            public Vector2 Size;
            public Vector2 Bearing;
            public float Advance;
        }
        
        public struct MetricsData
        {
            public int VerticesCount { get; set; }
            public int IndicesCount { get; set; }
            public int MeshCount { get; set; }
        }
        
        public PhiViewer Viewer { get; }
        
        public GraphicsDevice GraphicsDevice => Viewer.Host.Window.GraphicsDevice;

        public DisposeCollectorResourceFactory Factory { get; private set; }

        public CommandList CommandList { get; private set; }

        private Matrix4x4 _transform;
        
        private Shader[] _shaders;
        private Shader[] _shadersClip;
        private ResourceLayout _fragLayout;
        private ResourceLayout _fragLayoutClip;
        private ResourceLayout _vertLayout;
        private VertexLayoutDescription _vertexLayoutDesc;
        
        // Clip framebuffer
        private List<ClipStateEntry> _clipStack = new List<ClipStateEntry>();
        private int _clipLevel;

        private Stack<FilterDescription> _filters = new Stack<FilterDescription>();
        
        private Texture _renderColor;
        private Texture _renderColorResolved;
        private Texture _renderDepth;
        
        private Framebuffer _renderTarget;
        private Pipeline _pipeline;
        private GraphicsPipelineDescription _pipelineDescription;

        internal Texture RenderTargetTexture => _renderColor;
        
        internal Texture ResolvedRenderTargetTexture => _renderColorResolved;
        internal Texture RenderTargetDepth => _renderDepth;
        internal Texture ClipBufferTexture => _renderDepth;
        internal Texture ClipBufferDepth => _renderDepth;

        private bool _isClipping;

        private FreeTypeLibrary _ft;
        private IntPtr _fontFace = IntPtr.Zero;
        private IntPtr _memFont = IntPtr.Zero;

        private uint _fontSize = 70;
        private uint _fontTexPad = 5;
        private uint _fontRes = 300;
        private Dictionary<char, DrawableCharacter> _characters = new Dictionary<char, DrawableCharacter>();
        
        // Metrics
        private MetricsData _metrics;
        public MetricsData Metrics => _metrics;

        public bool NeedsUpdateRenderTarget { get; set; }

        private PipelineProfile _currentProfile;

        public PipelineProfile CurrentProfile
        {
            get => _currentProfile;
            set => SetCurrentPipelineProfile(value);
        }
        
        public PipelineProfile BasicNormal { get; private set; }
        public PipelineProfile BasicAdditive { get; private set; }
        
        private GraphicsPipelineDescription _clipPD;
        private PipelineProfile _mainSwapchainProfile;

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
            Factory = new DisposeCollectorResourceFactory(GraphicsDevice.ResourceFactory);

            var vertexLayout = new VertexLayoutDescription(
                new VertexElementDescription("Position", VertexElementSemantic.TextureCoordinate, VertexElementFormat.Float3),
                new VertexElementDescription("TextureCoordinates", VertexElementSemantic.TextureCoordinate, VertexElementFormat.Float2));
            _vertexLayoutDesc = vertexLayout;

            var asm = ResourceHelper.Assembly;
            using var fsh = asm.GetManifestResourceStream("Phi.Resources.Shaders.Textured.fsh");
            using var fshClip = asm.GetManifestResourceStream("Phi.Resources.Shaders.ClipMap.fsh");
            using var vsh = asm.GetManifestResourceStream("Phi.Resources.Shaders.Basic.vsh");

            var fshBuffer = new byte[fsh.Length];
            fsh.Read(fshBuffer, 0, fshBuffer.Length);
            var fshClipBuffer = new byte[fshClip.Length];
            fshClip.Read(fshClipBuffer, 0, fshClipBuffer.Length);
            var vshBuffer = new byte[vsh.Length];
            vsh.Read(vshBuffer, 0, vshBuffer.Length);
            
            var vertexShaderDesc = new ShaderDescription(ShaderStages.Vertex, vshBuffer, "main");
            var fragmentShaderDesc = new ShaderDescription(ShaderStages.Fragment, fshBuffer, "main");
            var fragmentClipShaderDesc = new ShaderDescription(ShaderStages.Fragment, fshClipBuffer, "main");

            _shaders = Factory.CreateFromSpirv(vertexShaderDesc, fragmentShaderDesc);
            _shadersClip = Factory.CreateFromSpirv(vertexShaderDesc, fragmentClipShaderDesc);

            _vertLayout = Factory.CreateResourceLayout(new ResourceLayoutDescription(
                new ResourceLayoutElementDescription("Transform", ResourceKind.UniformBuffer, ShaderStages.Vertex)));
            
            _fragLayout = Factory.CreateResourceLayout(new ResourceLayoutDescription(
                new ResourceLayoutElementDescription("Input", ResourceKind.TextureReadOnly, ShaderStages.Fragment),
                new ResourceLayoutElementDescription("Sampler", ResourceKind.Sampler, ShaderStages.Fragment),
                new ResourceLayoutElementDescription("Tint", ResourceKind.UniformBuffer, ShaderStages.Fragment),
                new ResourceLayoutElementDescription("ClipMap", ResourceKind.TextureReadOnly, ShaderStages.Fragment),
                new ResourceLayoutElementDescription("ClipMapSampler", ResourceKind.Sampler, ShaderStages.Fragment),
                new ResourceLayoutElementDescription("Filters", ResourceKind.UniformBuffer, ShaderStages.Fragment)
                ));
            
            _fragLayoutClip = Factory.CreateResourceLayout(new ResourceLayoutDescription
            {
                Elements = Array.Empty<ResourceLayoutElementDescription>()
            });
            
            
            // Main swapchain
            var mainPd = new GraphicsPipelineDescription();
            mainPd.BlendState = BlendStateDescription.SingleAlphaBlend;
            mainPd.DepthStencilState = new DepthStencilStateDescription(false, false, ComparisonKind.Always,
                false, 
                new StencilBehaviorDescription(StencilOperation.Keep, StencilOperation.Keep, StencilOperation.Keep, ComparisonKind.Always),
                new StencilBehaviorDescription(StencilOperation.Keep, StencilOperation.Keep, StencilOperation.Keep, ComparisonKind.Always),
                0, 0, 0);
            mainPd.RasterizerState = new RasterizerStateDescription(FaceCullMode.None, PolygonFillMode.Solid, FrontFace.Clockwise, false, false);
            mainPd.PrimitiveTopology = PrimitiveTopology.TriangleStrip;
            mainPd.ResourceLayouts = new [] { _vertLayout, _fragLayout };
            mainPd.ShaderSet = new ShaderSetDescription(
                vertexLayouts: new[] { vertexLayout },
                shaders: _shaders);

            mainPd.Outputs = GraphicsDevice.SwapchainFramebuffer.OutputDescription;
            var mainPipeline = Factory.CreateGraphicsPipeline(mainPd);
            mainPipeline.Name = "Main Swapchain Pipeline";

            _mainSwapchainProfile = new PipelineProfile
            {
                Framebuffer = GraphicsDevice.SwapchainFramebuffer,
                Pipeline = mainPipeline,
                PipelineDescription = mainPd
            };

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

            CommandList = Factory.CreateCommandList();

            Factory = new DisposeCollectorResourceFactory(GraphicsDevice.ResourceFactory);
            UpdateRenderTarget();
            
            // FreeType
            var fontStream = asm.GetManifestResourceStream("Phi.Resources.Fonts.Saira.ttf");
            var buffer = new byte[fontStream!.Length];
            fontStream.Read(buffer, 0, buffer.Length);
            fontStream.Dispose();
            
            _memFont = Marshal.AllocHGlobal(buffer.Length);
            Marshal.Copy(buffer, 0, _memFont, buffer.Length);

            _ft = new FreeTypeLibrary();
            if (FT_New_Memory_Face(_ft.Native, _memFont, buffer.Length, 0, out _fontFace) != FT_Error.FT_Err_Ok)
            {
                _ft.Dispose();
                _ft = null;
            }
            else
            {
                FT_Set_Char_Size(_fontFace, IntPtr.Zero, new IntPtr(_fontSize * 64), _fontRes, _fontRes);
            }
        }

        public GraphicsPipelineDescription CreateBasicDescription()
        {
            return new GraphicsPipelineDescription
            {
                BlendState = new BlendStateDescription
                {
                    AttachmentStates = new[]
                    {
                        new BlendAttachmentDescription
                        {
                            BlendEnabled = true,
                            SourceColorFactor = BlendFactor.SourceAlpha,
                            DestinationColorFactor = BlendFactor.InverseSourceAlpha,
                            ColorFunction = BlendFunction.Add,
                            AlphaFunction = BlendFunction.Add,
                            SourceAlphaFactor = BlendFactor.One,
                            DestinationAlphaFactor = BlendFactor.One
                        }
                    }
                },
                DepthStencilState = new DepthStencilStateDescription(false, false, ComparisonKind.Always,
                    false, 
                    new StencilBehaviorDescription(StencilOperation.Keep, StencilOperation.Keep, StencilOperation.Keep, ComparisonKind.Always),
                    new StencilBehaviorDescription(StencilOperation.Keep, StencilOperation.Keep, StencilOperation.Keep, ComparisonKind.Always),
                    0, 0, 0),
                RasterizerState = new RasterizerStateDescription(FaceCullMode.None, PolygonFillMode.Solid, 
                    FrontFace.Clockwise, false, false),
                PrimitiveTopology = PrimitiveTopology.TriangleList,
                ResourceLayouts = new [] { _vertLayout, _fragLayout },
                ShaderSet = new ShaderSetDescription(
                    vertexLayouts: new[] { _vertexLayoutDesc },
                    shaders: _shaders)
            };
        }

        private void UpdateRenderTarget()
        {
            var factory = Factory;
            var window = Viewer.Host.Window;
            var resolutionScale = 1f;

            var targetWidth = GraphicsDevice.SwapchainFramebuffer.Width; // (uint) (window.Width * resolutionScale);
            var targetHeight = GraphicsDevice.SwapchainFramebuffer.Height; // (uint) (window.Height * resolutionScale);
            
            if (!NeedsUpdateRenderTarget && _renderColor?.Width == targetWidth
                && _renderColor?.Height == targetHeight) return;

            NeedsUpdateRenderTarget = false;

            // Textures
            var renderColorDesc = TextureDescription.Texture2D(
                targetWidth, targetHeight,
                1, 1, PixelFormat.R8_G8_B8_A8_UNorm,
                TextureUsage.RenderTarget | TextureUsage.Sampled);
            
            var sampleCount = TextureSampleCount.Count16;
            GraphicsDevice.GetPixelFormatSupport(renderColorDesc.Format, renderColorDesc.Type, renderColorDesc.Usage,
                out var properties);
            while (!properties.IsSampleCountSupported(sampleCount)) sampleCount--;
            renderColorDesc.SampleCount = sampleCount;
            
            _renderColor?.Dispose();
            _renderColor = factory.CreateTexture(renderColorDesc);
            _renderColor.Name = "Render Target Color";
            factory.DisposeCollector.Remove(_renderColor);

            _renderColorResolved?.Dispose();
            if (sampleCount != TextureSampleCount.Count1)
            {
                renderColorDesc.SampleCount = TextureSampleCount.Count1;
                _renderColorResolved = factory.CreateTexture(renderColorDesc);
                _renderColorResolved.Name = "Resolved Render Target Color";
                _renderColor.Name = "Render Target Color";
            }
            else
            {
                _renderColorResolved = factory.CreateTexture(renderColorDesc);
                _renderColorResolved.Name = "Resolved Render Target Color";
            }
            factory.DisposeCollector.Remove(_renderColorResolved);

            _renderDepth?.Dispose();
            _renderDepth = factory.CreateTexture(TextureDescription.Texture2D(
                targetWidth, targetHeight, 
                1, 1, PixelFormat.R16_UNorm,
                TextureUsage.DepthStencil | TextureUsage.Sampled, 
                sampleCount));
            _renderDepth.Name = "Render Target Depth";
            factory.DisposeCollector.Remove(_renderDepth);
            
            // Pipeline: Normal
            BasicNormal ??= new PipelineProfile
            {
                PipelineDescription = CreateBasicDescription()
            };
            
            BasicNormal.DisposeResources();

            var renderTarget = factory.CreateFramebuffer(new FramebufferDescription(_renderDepth, _renderColor));
            renderTarget.Name = "Render Target Framebuffer";
            factory.DisposeCollector.Remove(renderTarget);
            
            var renderPd = BasicNormal.PipelineDescription;
            renderPd.Outputs = renderTarget.OutputDescription;
            BasicNormal.PipelineDescription = renderPd;
            
            var pipeline = factory.CreateGraphicsPipeline(renderPd);
            pipeline.Name = "Render Target Pipeline";
            factory.DisposeCollector.Remove(pipeline);

            BasicNormal.Framebuffer = renderTarget;
            BasicNormal.Pipeline = pipeline;

            // Pipeline: Additive
            if (BasicAdditive == null)
            {
                renderPd = CreateBasicDescription();
                renderPd.BlendState.AttachmentStates[0].SourceColorFactor = BlendFactor.SourceAlpha;
                renderPd.BlendState.AttachmentStates[0].DestinationColorFactor = BlendFactor.DestinationAlpha;
                BasicAdditive = new PipelineProfile
                {
                    PipelineDescription = renderPd
                };
            }
            else
            {
                renderPd = BasicAdditive.PipelineDescription;
            }
            BasicAdditive.Pipeline?.Dispose();   // Don't call DisposeResources() here from now on
            BasicAdditive.Framebuffer = BasicNormal.Framebuffer;
            renderPd.Outputs = renderTarget.OutputDescription;
            
            pipeline = factory.CreateGraphicsPipeline(renderPd);
            pipeline.Name = "Render Target Pipeline (Additive)";
            factory.DisposeCollector.Remove(pipeline);
            BasicAdditive.Pipeline = pipeline;

            // Pipeline: Main
            _mainSwapchainProfile.Pipeline?.Dispose();
            renderPd = _mainSwapchainProfile.PipelineDescription;
            renderPd.Outputs = GraphicsDevice.SwapchainFramebuffer.OutputDescription;
            var mp = factory.CreateGraphicsPipeline(renderPd);
            factory.DisposeCollector.Remove(mp);
            _mainSwapchainProfile.Framebuffer = GraphicsDevice.SwapchainFramebuffer;
            _mainSwapchainProfile.Pipeline = mp;
            
            // Clip
            for (var i = 0; i < _clipStack.Count; i++)
            {
                _clipStack[i].Dispose();
                _clipStack[i] = CreateClipEntry();
            }

            if (!_clipStack.Any())
            {
                _clipStack.Add(CreateClipEntry());
            }
        }

        private ClipStateEntry CreateClipEntry()
        {
            var factory = Factory;
            var window = Viewer.Host.Window;
            var resolutionScale = 1f;

            var targetWidth = GraphicsDevice.SwapchainFramebuffer.Width;
            var targetHeight = GraphicsDevice.SwapchainFramebuffer.Height;
            
            var clipColor = factory.CreateTexture(TextureDescription.Texture2D(
                targetWidth, targetHeight,
                1, 1, PixelFormat.R8_G8_B8_A8_UNorm,
                TextureUsage.RenderTarget | TextureUsage.Sampled));
            clipColor.Name = $"Clip Map Color (depth:{_clipStack.Count})";
            
            var clipDepth = factory.CreateTexture(TextureDescription.Texture2D(
                targetWidth, targetHeight,
                1, 1, PixelFormat.D24_UNorm_S8_UInt,
                TextureUsage.DepthStencil | TextureUsage.Sampled));
            clipDepth.Name = $"Clip Map Depth (depth:{_clipStack.Count})";

            var clipFramebuffer = factory.CreateFramebuffer(new FramebufferDescription(clipDepth, clipColor));
            clipFramebuffer.Name = $"Clip Map Framebuffer (depth:{_clipStack.Count})";

            _clipPD.Outputs = clipFramebuffer.OutputDescription;
            var clipPipeline = factory.CreateGraphicsPipeline(_clipPD);
            clipPipeline.Name = $"Clip Map Pipeline (depth:{_clipStack.Count})";
            
            factory.DisposeCollector.Remove(clipColor);
            factory.DisposeCollector.Remove(clipDepth);
            factory.DisposeCollector.Remove(clipPipeline);
            factory.DisposeCollector.Remove(clipFramebuffer);

            return new ClipStateEntry
            {
                Profile = new PipelineProfile
                {
                    Framebuffer = clipFramebuffer,
                    Pipeline = clipPipeline,
                    PipelineDescription = _clipPD
                },
                Texture = clipColor,
                Depth = clipDepth
            };
        }

        public void Begin()
        {
            var window = Viewer.Host.Window;

            var fb = GraphicsDevice.MainSwapchain.Framebuffer;
            if (fb.Width != (uint)window.Width || fb.Height != (uint)window.Height)
            {
                GraphicsDevice.MainSwapchain.Resize((uint)window.Width, (uint)window.Height);
            }
            UpdateRenderTarget();

            _metrics = new MetricsData();
            CommandList.Begin();
        }

        public void SetFullViewport()
        {
            var window = Viewer.Host.Window;
            CommandList.SetViewport(0, new Viewport(0, 0, window.Width, window.Height, 0, 1));
        }

        public void SetCurrentPipelineProfile(PipelineProfile profile)
        {
            CommandList.SetFramebuffer(profile.Framebuffer);
            CommandList.SetPipeline(profile.Pipeline);
            _currentProfile = profile;
        }

        public void ClearBuffers()
        {
            ClearClip();
            CommandList.SetFramebuffer(GraphicsDevice.SwapchainFramebuffer);
            CommandList.ClearColorTarget(0, RgbaFloat.Black);
            CommandList.SetFramebuffer(BasicNormal.Framebuffer);
            CommandList.ClearColorTarget(0, RgbaFloat.Black);
            if (_currentProfile != null) SetCurrentPipelineProfile(_currentProfile);
        }

        /// <summary>
        /// Draws a quad with currently used pipeline, framebuffer and fragment resource set.
        /// </summary>
        /// <param name="x">The X position (left-top) of the quad.</param>
        /// <param name="y">The Y position (left-top) of the quad.</param>
        /// <param name="width">The width of the quad.</param>
        /// <param name="height">The height of the quad.</param>
        public void DrawQuad(float x, float y, float width, float height, Vector2[] uv = null)
        {
            Vector3[] quadMesh =
            {
                new Vector3(x, y, 0),
                new Vector3(x + width, y, 0),
                new Vector3(x, y + height, 0),
                new Vector3(x + width, y + height, 0)
            };
            
            var quadUv = uv ?? new[]
            {
                new Vector2(0, 0),
                new Vector2(1, 0),
                new Vector2(0, 1),
                new Vector2(1, 1)
            };
            
            ushort[] quadIndices = { 0, 1, 2, 2, 1, 3 };
            
            DrawMesh(quadMesh, quadUv, quadIndices);
        }

        public void DrawMesh(Vector3[] mesh, Vector2[] uv, ushort[] indices)
        {
            if (mesh.Length != uv.Length)
                throw new ArgumentException("UV length doesn't match the mesh length");

            var vertices = new VertexInfo[mesh.Length];
            for (var i = 0; i < mesh.Length; i++)
            {
                vertices[i] = new VertexInfo
                {
                    Position = mesh[i],
                    Uv = uv[i]
                };
            }

            float boundMinX, boundMinY, boundMaxX, boundMaxY;
            if (mesh.Length == 0) return;
            
            var transformed = mesh
                .Select(v => Vector3.Transform(v, _transform) + new Vector3(_transform.M14, _transform.M24, _transform.M34))
                .ToList();
            boundMinX = boundMaxX = transformed[0].X;
            boundMinY = boundMaxY = transformed[0].Y;
            
            foreach (var v in transformed.Skip(1))
            {
                boundMinX = MathF.Min(v.X, boundMinX);
                boundMaxX = MathF.Max(v.X, boundMaxX);
                boundMinY = MathF.Min(v.Y, boundMinY);
                boundMaxY = MathF.Max(v.Y, boundMaxY);
            }

            var bound = new RectangleF(boundMinX, boundMinY, boundMaxX - boundMinX, boundMaxY - boundMinY);
            var view = new RectangleF(0, 0, Viewer.WindowSize.Width, Viewer.WindowSize.Height);
            if (!view.IntersectsWith(bound)) return;
            
            CommandList.PushDebugGroup("DrawMesh");
            var vbDescription = new BufferDescription((uint)(20 * mesh.Length), BufferUsage.VertexBuffer);
            var vertexBuffer = Factory.CreateBuffer(vbDescription);
            vertexBuffer.Name = "Vertex Buffer";
            GraphicsDevice.UpdateBuffer(vertexBuffer, 0, vertices);
            CommandList.SetVertexBuffer(0, vertexBuffer);
            
            var ibDescription = new BufferDescription(
                (uint)indices.Length * sizeof(ushort),
                BufferUsage.IndexBuffer);
            var indexBuffer = Factory.CreateBuffer(ibDescription);
            indexBuffer.Name = "Index Buffer";
            GraphicsDevice.UpdateBuffer(indexBuffer, 0, indices);
            
            var matrixInfoBuffer = Factory.CreateBuffer(new BufferDescription(MatrixInfo.SizeInBytes, BufferUsage.UniformBuffer));
            matrixInfoBuffer.Name = "Vertex Layout - Matrix Info";
            GraphicsDevice.UpdateBuffer(
                matrixInfoBuffer, 0, new MatrixInfo
                {
                    Transform = _transform,
                    Resolution = new Vector2(Viewer.WindowSize.Width, Viewer.WindowSize.Height)
                });
            
            var resourceSet = Factory.CreateResourceSet(new ResourceSetDescription(_vertLayout, matrixInfoBuffer));
            resourceSet.Name = "Vertex Layout Resource Set";
            
            CommandList.SetGraphicsResourceSet(0, resourceSet);
            CommandList.SetIndexBuffer(indexBuffer, IndexFormat.UInt16);
            CommandList.DrawIndexed((uint)indices.Length);
            CommandList.PopDebugGroup();

            _metrics.IndicesCount += indices.Length;
            _metrics.MeshCount++;
            _metrics.VerticesCount += vertices.Length;
        }

        public void DrawTexture(Texture texture, float x, float y, float width, float height, TintInfo? tintInfo = null)
        {
            CommandList.PushDebugGroup("DrawTexture");
            var factory = Factory;
            var view = factory.CreateTextureView(texture);
            view.Name = texture.Name + " (Generated View)";
            DrawTexture(view, x, y, width, height, tintInfo);
            CommandList.PopDebugGroup();
        }

        public void ResolveTexture()
        {
            if (_renderColor.SampleCount != TextureSampleCount.Count1)
            {
                CommandList.ResolveTexture(_renderColor, _renderColorResolved);
            }
            else
            {
                CommandList.CopyTexture(_renderColor, _renderColorResolved);
            }
        }

        public void DrawToMainSwapchain()
        {
            CommandList.PushDebugGroup("DrawToMainSwapchain");
            SetCurrentPipelineProfile(_mainSwapchainProfile);
            CommandList.ClearColorTarget(0, RgbaFloat.Black);
            DrawTexture(_renderColorResolved, 0, 0, Viewer.WindowSize.Width, Viewer.WindowSize.Height);
            CommandList.PopDebugGroup();
        }

        private DeviceBuffer CreateFilterBuffer()
        {
            var factory = Factory;
            var buffer = factory.CreateBuffer(new BufferDescription(16, BufferUsage.UniformBuffer));
            GraphicsDevice.UpdateBuffer(buffer, 0, _filters.Count == 0 ? new FilterDescription() : _filters.Peek());
            return buffer;
        }
        
        public void DrawTexture(TextureView view, float x, float y, float width, float height, TintInfo? tintInfo = null)
        {
            CommandList.PushDebugGroup("DrawTextureView");
            var factory = Factory;
            
            var tintInfoBuffer = factory.CreateBuffer(new BufferDescription(32, BufferUsage.UniformBuffer));
            tintInfoBuffer.Name = "Texture TintInfo Uniform Buffer";
            GraphicsDevice.UpdateBuffer(
                tintInfoBuffer, 0,
                tintInfo ?? new TintInfo(new Vector3(1f, 1f, 1f), 0f, 1f));

            var filtersBuffer = CreateFilterBuffer();
            var resourceSet = factory.CreateResourceSet(new ResourceSetDescription(
                _fragLayout, view, GraphicsDevice.LinearSampler, tintInfoBuffer, 
                _clipStack[_clipLevel].Texture, GraphicsDevice.LinearSampler, filtersBuffer));
            resourceSet.Name = "Texture Fragment Resource Set";
            CommandList.SetGraphicsResourceSet(1, resourceSet);
            
            DrawQuad(x, y, width, height);
            CommandList.PopDebugGroup();
        }
        
        public void DrawRect(Color color, float x, float y, float width, float height)
        {
            CommandList.PushDebugGroup("DrawRect");
            PrepareSolidColorResourceSet(color);
            DrawQuad(x, y, width, height);
            CommandList.PopDebugGroup();
        }

        private void PrepareSolidColorResourceSet(Color color)
        {
            var factory = Factory;
            var tintInfoBuffer = factory.CreateBuffer(new BufferDescription(32, BufferUsage.UniformBuffer));
            tintInfoBuffer.Name = "Solid Color TintInfo Uniform Buffer";
            GraphicsDevice.UpdateBuffer(
                tintInfoBuffer, 0,
                new TintInfo(new Vector3(color.R / 255f, color.G / 255f, color.B / 255f), 1, color.A / 255f));

            var tex = factory.CreateTexture(new TextureDescription(1, 1, 1, 1, 1, PixelFormat.R8_G8_B8_A8_UNorm,
                TextureUsage.Sampled, TextureType.Texture2D));
            tex.Name = "Solid Color Texture";
            var view = factory.CreateTextureView(tex);
            view.Name = "Solid Color TextureView";

            ResourceSet resourceSet;
            if (!_isClipping)
            {
                var filtersBuffer = CreateFilterBuffer();
                resourceSet = factory.CreateResourceSet(new ResourceSetDescription(
                    _fragLayout, view, GraphicsDevice.LinearSampler, tintInfoBuffer, 
                    _clipStack[_clipLevel].Texture,
                    GraphicsDevice.LinearSampler, filtersBuffer));
                resourceSet.Name = "Solid Color Fragment Resource Set";
                CommandList.SetGraphicsResourceSet(1, resourceSet);
            }
            else
            {
                resourceSet = factory.CreateResourceSet(new ResourceSetDescription(_fragLayoutClip));
                resourceSet.Name = "Clip Map Fragment Resource Set";
                CommandList.SetGraphicsResourceSet(1, resourceSet);
            }
        }

        public void StrokeRect(Color color, float x, float y, float width, float height, float thickness)
        {
            CommandList.PushDebugGroup("StrokeRect");
            Vector3[] mesh =
            {
                // Outer
                /* 0 */ new Vector3(x - thickness / 2,         y - thickness / 2, 0),
                /* 1 */ new Vector3(x + thickness / 2 + width, y - thickness / 2, 0),
                /* 2 */ new Vector3(x - thickness / 2,         y + thickness / 2 + height, 0),
                /* 3 */ new Vector3(x + thickness / 2 + width, y + thickness / 2 + height, 0),
                
                // Inner
                /* 4 */ new Vector3(x + thickness / 2,         y + thickness / 2, 0),
                /* 5 */ new Vector3(x - thickness / 2 + width, y + thickness / 2, 0),
                /* 6 */ new Vector3(x + thickness / 2,         y - thickness / 2 + height, 0),
                /* 7 */ new Vector3(x - thickness / 2 + width, y - thickness / 2 + height, 0),
            };
            
            var uv = new[]
            {
                new Vector2(0, 0),
                new Vector2(1, 0),
                new Vector2(0, 1),
                new Vector2(1, 1),
                
                new Vector2(0, 0),
                new Vector2(1, 0),
                new Vector2(0, 1),
                new Vector2(1, 1),
            };

            /*
             * 0       1
             *   4   5
             *
             *   6   7
             * 2       3
             */
            // 0 1 2 2 1 3
            var indices = new ushort[]
            {
                0, 1, 4, 4, 1, 5, // up
                0, 4, 2, 2, 4, 6, // left
                5, 1, 7, 7, 1, 3, // right
                6, 7, 2, 2, 7, 3 // bottom
            };

            PrepareSolidColorResourceSet(color);
            DrawMesh(mesh, uv, indices);
            CommandList.PopDebugGroup();
        }

        public void StrokeArc(Color color, float x, float y, float radius, float startRadians, float endRadians, float thickness, bool counterClockwise = false)
        {
            CommandList.PushDebugGroup("StrokeArc");
            var s = startRadians;
            var e = endRadians;
            if (counterClockwise) (s, e) = (e, s);

            if (e - s > MathF.PI * 2) e -= MathF.PI * 2;
            var sections = (int) Math.Ceiling(radius * (e - s) / 18);
            StrokeSectionedArc(color, x, y, radius, startRadians, endRadians, sections, thickness, counterClockwise);
            CommandList.PopDebugGroup();
        }
        
        public void StrokeSectionedArc(Color color, float x, float y, float radius, float startRadians, float endRadians, int sections, float thickness, bool counterClockwise = false)
        {
            CommandList.PushDebugGroup($"StrokeSectionedArc ({sections} sections)");
            if (counterClockwise)
            {
                (startRadians, endRadians) = (endRadians, startRadians);
            }
            if (endRadians < startRadians) endRadians += MathF.PI * 2;

            var vertices = new List<Vector3>();
            var uv = new List<Vector2>();
            var indices = new List<ushort>();
            var indexCount = 0;
            var sectionSize = (endRadians - startRadians) / sections;
            for (var i = startRadians; i < endRadians; i += sectionSize)
            {
                if (Math.Abs(i - endRadians) < 0.01f) break;
                
                vertices.Add(new Vector3(
                    x + MathF.Cos(i) * (radius + thickness / 2),
                    y + MathF.Sin(i) * (radius + thickness / 2), 0));

                vertices.Add(new Vector3(
                    x + MathF.Cos(i) * (radius - thickness / 2),
                    y + MathF.Sin(i) * (radius - thickness / 2), 0));
                    
                vertices.Add(new Vector3(
                    x + MathF.Cos(i + sectionSize) * (radius + thickness / 2),
                    y + MathF.Sin(i + sectionSize) * (radius + thickness / 2), 0));
                
                vertices.Add(new Vector3(
                    x + MathF.Cos(i + sectionSize) * (radius - thickness / 2),
                    y + MathF.Sin(i + sectionSize) * (radius - thickness / 2), 0));
                
                indices.AddRange(new[]
                {
                    (ushort) (indexCount + 0),
                    (ushort) (indexCount + 2),
                    (ushort) (indexCount + 1),
                    (ushort) (indexCount + 1),
                    (ushort) (indexCount + 2),
                    (ushort) (indexCount + 3),
                });
                
                uv.AddRange(new []
                {
                    new Vector2(0, 0),
                    new Vector2(1, 0),
                    new Vector2(0, 1),
                    new Vector2(1, 1),
                });

                indexCount += 4;
            }

            PrepareSolidColorResourceSet(color);
            DrawMesh(vertices.ToArray(), uv.ToArray(), indices.ToArray());
            CommandList.PopDebugGroup();
        }
        
        public void DrawSectorSmooth(Color color, float x, float y, float radius, float startRadians, float endRadians, bool counterClockwise = false)
        {
            CommandList.PushDebugGroup("DrawSectorSmooth");
            var s = startRadians;
            var e = endRadians;
            if (counterClockwise) (s, e) = (e, s);

            if (e - s > MathF.PI * 2) e -= MathF.PI * 2;
            var sections = (int) Math.Ceiling(radius * (e - s) / 9);
            DrawSector(color, x, y, radius, startRadians, endRadians, sections, counterClockwise);
            CommandList.PopDebugGroup();
        }
        
        public void DrawSector(Color color, float x, float y, float radius, float startRadians, float endRadians, int sections, bool counterClockwise = false)
        {
            CommandList.PushDebugGroup($"DrawSector ({sections} sections)");
            if (counterClockwise)
            {
                (startRadians, endRadians) = (endRadians, startRadians);
            }
            if (endRadians < startRadians) endRadians += MathF.PI * 2;

            var vertices = new List<Vector3>();
            var uv = new List<Vector2>();
            var indices = new List<ushort>();
            var indexCount = 0;
            var sectionSize = (endRadians - startRadians) / sections;
            for (var i = startRadians; i < endRadians; i += sectionSize)
            {
                if (Math.Abs(i - endRadians) < 0.01f) break;
                
                vertices.Add(new Vector3(
                    x + MathF.Cos(i) * radius,
                    y + MathF.Sin(i) * radius, 0));

                vertices.Add(new Vector3(
                    x + MathF.Cos(i + sectionSize) * radius,
                    y + MathF.Sin(i + sectionSize) * radius, 0));
                
                vertices.Add(new Vector3(x, y, 0));
                
                indices.AddRange(new[]
                {
                    (ushort) (indexCount + 0),
                    (ushort) (indexCount + 1),
                    (ushort) (indexCount + 2)
                });
                
                uv.AddRange(new []
                {
                    new Vector2(0, 0),
                    new Vector2(1, 0),
                    new Vector2(0, 1),
                });

                indexCount += 3;
            }

            PrepareSolidColorResourceSet(color);
            DrawMesh(vertices.ToArray(), uv.ToArray(), indices.ToArray());
            CommandList.PopDebugGroup();
        }
        
        public void Render()
        {
            CommandList.End();
            GraphicsDevice.SubmitCommands(CommandList);
            GraphicsDevice.WaitForIdle();
            GraphicsDevice.SwapBuffers();
            
            Factory.DisposeCollector.DisposeAll();
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

        public void PushClip()
        {
            CommandList.PushDebugGroup("Clip");
            var factory = Factory;
            var old = _clipStack[_clipLevel].Texture;
            
            if (_clipStack.Count != ++_clipLevel)
            {
                var newClip = _clipStack[_clipLevel].Texture;
                CommandList.CopyTexture(old, newClip);
                return;
            }

            var entry = CreateClipEntry();
            CommandList.CopyTexture(old, entry.Texture);
            _clipStack.Add(entry);
        }

        public void PopClip()
        {
            if (_clipLevel == 0) return;
            _clipLevel--;
            CommandList.PopDebugGroup();
        }

        public void ClipRect(float x, float y, float width, float height)
        {
            CommandList.PushDebugGroup("ClipRect");
            _isClipping = true;
            
            var entry = _clipStack[_clipLevel];
            CommandList.SetFramebuffer(entry.Profile.Framebuffer);
            CommandList.SetPipeline(entry.Profile.Pipeline);

            var p = Viewer.WindowSize.Width + Viewer.WindowSize.Height;
            p += width + height;

            var t = Transform;
            if (GraphicsDevice.BackendType != GraphicsBackend.OpenGL &&
                GraphicsDevice.BackendType != GraphicsBackend.OpenGLES)
            {
                Transform = Matrix4x4.Identity;
                Scale(1, -1);
                Translate(0, -Viewer.WindowSize.Height);
                Transform *= t;
            }
            
            DrawRect(Color.Black, x - p, y,       p, p);
            DrawQuad(x, y + height,               p, p);
            DrawQuad(x + width, y + height - p, p, p);
            DrawQuad(x - p, y - p,              p * 2, p);

            Transform = t;
            _isClipping = false;
            SetCurrentPipelineProfile(_currentProfile);
            CommandList.PopDebugGroup();
        }
        
        public void ClearClip()
        {
            var entry = _clipStack[_clipLevel];
            CommandList.SetFramebuffer(entry.Profile.Framebuffer);
            CommandList.ClearColorTarget(0, RgbaFloat.White);
            if (_currentProfile != null) SetCurrentPipelineProfile(_currentProfile);
        }

        public SizeF MeasureText(string text, float fontSize)
        {
            var x = 0f;
            var y = 0f;
            var scale = fontSize / _fontSize * 72 / _fontRes;

            char? lastChar = null;
            foreach (var c in text)
            {
                var chn = GetDrawableCharacter(c);
                if (chn == null) continue;

                var ch = chn.Value;
                var h = ch.Size.Y * scale;
                AdvanceX(lastChar, c, ref x, scale);
                lastChar = c;
                y = MathF.Max(y, h);
            }
            
            return new SizeF(x, y);
        }

        private bool AdvanceX(char? lastChar, char c, ref float x, float scale)
        {
            var chn = GetDrawableCharacter(c);
            if (chn == null) return false;

            var ch = chn.Value;
            x += c == ' ' ? 87 * scale : ch.Advance * scale;
            if (lastChar.HasValue) x += GetKerning(lastChar.Value, c).X * scale;
            return true;
        }

        public void ClearFontCache()
        {
            var values = _characters.Values;
            _characters.Clear();
            foreach (var c in values)
            {
                c.Texture.Dispose();
            }
            GC.Collect();
        }

        public int CachedFontGlyphs => _characters.Count;
        
        private unsafe DrawableCharacter? GetDrawableCharacter(char c)
        {
            var factory = Factory;
            if (!_characters.ContainsKey(c))
            {
                if (FT_Load_Char(_fontFace, c, FT_LOAD_RENDER) != FT_Error.FT_Err_Ok) return null;

                var face = new FreeTypeFaceFacade(_ft, _fontFace);
                var w = Math.Max(1, face.GlyphBitmap.width);
                var h = Math.Max(1, face.GlyphBitmap.rows);
                
                var texture = factory.CreateTexture(new TextureDescription(
                    w + _fontTexPad * 2, h + _fontTexPad * 2, 1, 1, 1, PixelFormat.R8_G8_B8_A8_UNorm,
                    TextureUsage.Sampled, TextureType.Texture2D));
                texture.Name = $"Saira-char: {c}";
                factory.DisposeCollector.Remove(texture);

                var size = (w + _fontTexPad * 2) * (h + _fontTexPad * 2) * 4;
                var buffer = new byte[size];

                for (var i = 0; i < size; i++)
                {
                    buffer[i] = (byte) (i % 4 == 3 ? 0 : 255);
                }
                
                for (var ty = 0; ty < face.GlyphBitmap.rows; ty++)
                {
                    for (var tx = 0; tx < face.GlyphBitmap.width; tx++)
                    {
                        var px = Marshal.ReadByte(face.GlyphBitmap.buffer, (int) (ty * face.GlyphBitmap.width +
                            tx));
                        var idx = (ty + _fontTexPad) * (w + _fontTexPad * 2) * 4 + (tx + _fontTexPad) * 4;
                        buffer[idx+0] = 255;
                        buffer[idx+1] = 255;
                        buffer[idx+2] = 255;
                        buffer[idx+3] = px;
                    }
                }

                CommandList.PushDebugGroup($"Generate Character Texture - {c}");
                GraphicsDevice.UpdateTexture(texture, buffer, 0, 0, 0, texture.Width,
                    texture.Height, 1, 0, 0);
                CommandList.PopDebugGroup();

                _characters.Add(c, new DrawableCharacter
                {
                    Texture = texture,
                    Size = new Vector2(w, h),
                    Bearing = new Vector2(face.GlyphBitmapLeft, face.GlyphBitmapTop),
                    Advance = face.FaceRec->glyph->advance.x.ToInt32() / 64f
                });
            }

            return _characters[c];
        }

        private Vector2 GetKerning(char left, char right)
        {
            FT_Get_Kerning(_fontFace, left, right, 0, out var vec);
            return new Vector2(vec.x.ToInt64() / 64f, vec.y.ToInt64() / 64f);
        }

        public void PushFilter(FilterDescription filters)
        {
            _filters.Push(filters);
        }

        public FilterDescription CopyCurrentFilter()
        {
            return _filters.Peek();
        }

        public FilterDescription PopFilter()
        {
            return _filters.Pop();
        }

        public void DrawText(string text, Color color, float fontSize, float x, float y)
        {
            if (_ft == null) return;
            
            var scale = fontSize / _fontSize * 72 / _fontRes;
            char? lastChar = null;
            
            CommandList.PushDebugGroup("DrawText");
            foreach (var c in text)
            {
                var chn = GetDrawableCharacter(c);
                if (chn == null) continue;

                var ch = chn.Value;
                var xPos = x + (ch.Bearing.X - _fontTexPad) * scale;
                var yPos = y - (ch.Bearing.Y + _fontTexPad) * scale;
                var w = (ch.Size.X + _fontTexPad * 2) * scale;
                var h = (ch.Size.Y + _fontTexPad * 2) * scale;
                AdvanceX(lastChar, c, ref x, scale);
                lastChar = c;

                if (c != ' ')
                {
                    CommandList.PushDebugGroup($"Character - {c}");
                    DrawTexture(ch.Texture, xPos, yPos, w, h,
                        new TintInfo(new Vector3(color.R / 255f, color.G / 255f, color.B / 255f), 0, color.A / 255f));
                    CommandList.PopDebugGroup();
                }
            }
            CommandList.PopDebugGroup();
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

    public struct FilterDescription
    {
        public float BlurRadius;
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

    public struct VertexInfo
    {
        public Vector3 Position;
        public Vector2 Uv;
    }
}