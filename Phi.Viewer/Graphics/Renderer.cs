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
using Color = System.Drawing.Color;
using ResourceSet = Veldrid.ResourceSet;

using static FreeTypeSharp.Native.FT;

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
        
        public PhiViewer Viewer { get; }
        
        public GraphicsDevice GraphicsDevice => Viewer.Host.Window.GraphicsDevice;

        public DisposeCollectorResourceFactory Factory { get; private set; }

        public CommandList CommandList { get; private set; }

        private Matrix4x4 _transform;

        
        private DeviceBuffer _quadIndexBuffer;
        
        private Shader[] _shaders;
        private Shader[] _shadersClip;
        private ResourceLayout _fragLayout;
        private ResourceLayout _fragLayoutClip;
        private ResourceLayout _vertLayout;
        private VertexLayoutDescription _vertexLayoutDesc;
        
        // Clip framebuffer
        private List<ClipStateEntry> _clipStack = new List<ClipStateEntry>();
        private int _clipLevel;
        
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
        private Dictionary<char, DrawableCharacter> _characters = new Dictionary<char, DrawableCharacter>();

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

            ushort[] quadIndices = { 0, 1, 2, 2, 1, 3 };
            var ibDescription = new BufferDescription(
                6 * sizeof(ushort),
                BufferUsage.IndexBuffer);
            _quadIndexBuffer = Factory.CreateBuffer(ibDescription);
            _quadIndexBuffer.Name = "Quad Index Buffer";
            GraphicsDevice.UpdateBuffer(_quadIndexBuffer, 0, quadIndices);

            var vertexLayout = new VertexLayoutDescription(
                new VertexElementDescription("Position", VertexElementSemantic.TextureCoordinate, VertexElementFormat.Float2),
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
                new ResourceLayoutElementDescription("ClipMapSampler", ResourceKind.Sampler, ShaderStages.Fragment)
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
                FT_Set_Pixel_Sizes(_fontFace, 0, _fontSize);
            }
        }

        private GraphicsPipelineDescription CreateBasicDescription()
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

            if (!NeedsUpdateRenderTarget && _renderColor?.Width == (uint)(window.Width * resolutionScale)
                && _renderColor?.Height == (uint)(window.Height * resolutionScale)) return;

            NeedsUpdateRenderTarget = false;

            // Textures
            var renderColorDesc = TextureDescription.Texture2D(
                (uint)(window.Width * resolutionScale),
                (uint)(window.Height * resolutionScale),
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
                (uint) (window.Width * resolutionScale), 
                (uint) (window.Height * resolutionScale), 
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
                renderPd.BlendState.AttachmentStates[0].SourceColorFactor = BlendFactor.One;
                renderPd.BlendState.AttachmentStates[0].DestinationColorFactor = BlendFactor.One;
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
            
            var clipColor = factory.CreateTexture(TextureDescription.Texture2D(
                (uint)(window.Width * resolutionScale),
                (uint)(window.Height * resolutionScale),
                1, 1, PixelFormat.R8_G8_B8_A8_UNorm,
                TextureUsage.RenderTarget | TextureUsage.Sampled));
            clipColor.Name = $"Clip Map Color (depth:{_clipStack.Count})";
            
            var clipDepth = factory.CreateTexture(TextureDescription.Texture2D(
                (uint)(window.Width * resolutionScale),
                (uint)(window.Height * resolutionScale),
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
        public void DrawQuad(float x, float y, float width, float height)
        {
            CommandList.PushDebugGroup("DrawQuad");
            Vector4[] quadVerts =
            {
                new Vector4(x, y, 0, 0),
                new Vector4(x + width, y, 1, 0),
                new Vector4(x, y + height, 0, 1),
                new Vector4(x + width, y + height, 1, 1),
            };
            var vbDescription = new BufferDescription(64, BufferUsage.VertexBuffer);
            
            var factory = Factory;
            var vertexBuffer = factory.CreateBuffer(vbDescription);
            vertexBuffer.Name = "Quad Vertex Buffer";
            GraphicsDevice.UpdateBuffer(vertexBuffer, 0, quadVerts);
            CommandList.SetVertexBuffer(0, vertexBuffer);
            
            var matrixInfoBuffer = factory.CreateBuffer(new BufferDescription(MatrixInfo.SizeInBytes, BufferUsage.UniformBuffer));
            matrixInfoBuffer.Name = "Quad Vertex Buffer - Matrix Info";
            GraphicsDevice.UpdateBuffer(
                matrixInfoBuffer, 0, new MatrixInfo
                {
                    Transform = _transform,
                    Resolution = new Vector2(Viewer.WindowSize.Width, Viewer.WindowSize.Height)
                });
            
            var resourceSet = factory.CreateResourceSet(new ResourceSetDescription(
                _vertLayout, matrixInfoBuffer));
            resourceSet.Name = "Quad Vertex Layout Resource Set";
            
            CommandList.SetGraphicsResourceSet(0, resourceSet);
            CommandList.SetIndexBuffer(_quadIndexBuffer, IndexFormat.UInt16);
            CommandList.DrawIndexed(6);
            CommandList.PopDebugGroup();
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

        public void DrawToMainSwapchain()
        {
            CommandList.PushDebugGroup("DrawToMainSwapchain");
            if (_renderColor.SampleCount != TextureSampleCount.Count1)
            {
                CommandList.ResolveTexture(_renderColor, _renderColorResolved);
            }
            else
            {
                CommandList.CopyTexture(_renderColor, _renderColorResolved);
            }

            SetCurrentPipelineProfile(_mainSwapchainProfile);
            CommandList.ClearColorTarget(0, RgbaFloat.Black);
            DrawTexture(_renderColorResolved, 0, 0, Viewer.WindowSize.Width, Viewer.WindowSize.Height);
            CommandList.PopDebugGroup();
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

            var resourceSet = factory.CreateResourceSet(new ResourceSetDescription(
                _fragLayout, view, GraphicsDevice.LinearSampler, tintInfoBuffer, _clipStack[_clipLevel].Texture, GraphicsDevice.LinearSampler));
            resourceSet.Name = "Texture Fragment Resource Set";
            CommandList.SetGraphicsResourceSet(1, resourceSet);
            
            DrawQuad(x, y, width, height);
            CommandList.PopDebugGroup();
        }
        
        public void DrawRect(Color color, float x, float y, float width, float height)
        {
            CommandList.PushDebugGroup("DrawRect");
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
                resourceSet = factory.CreateResourceSet(new ResourceSetDescription(
                    _fragLayout, view, GraphicsDevice.LinearSampler, tintInfoBuffer, _clipStack[_clipLevel].Texture,
                    GraphicsDevice.LinearSampler));
                resourceSet.Name = "Solid Color Fragment Resource Set";
                CommandList.SetGraphicsResourceSet(1, resourceSet);
            }
            else
            {
                resourceSet = factory.CreateResourceSet(new ResourceSetDescription(_fragLayoutClip));
                resourceSet.Name = "Clip Map Fragment Resource Set";
                CommandList.SetGraphicsResourceSet(1, resourceSet);
            }
            
            DrawQuad(x, y, width, height);
            CommandList.PopDebugGroup();
        }
        
        public void Render()
        {
            CommandList.End();
            GraphicsDevice.SubmitCommands(CommandList);
            GraphicsDevice.WaitForIdle();
            GraphicsDevice.SwapBuffers();
            
            Factory.DisposeCollector.DisposeAll();
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
            var scale = fontSize / _fontSize;
            
            foreach (var c in text)
            {
                var chn = GetDrawableCharacter(c);
                if (chn == null) continue;

                var ch = chn.Value;
                var yPos = y - ch.Bearing.Y * scale;
                var w = (ch.Size.X + _fontTexPad) * scale;
                var h = (ch.Size.Y + _fontTexPad) * scale;
                x += c == ' ' ? fontSize * 0.33f : w - _fontTexPad * scale + fontSize * 0.1f;
                y = MathF.Max(y, h);
            }
            
            return new SizeF(x, y);
        }

        private DrawableCharacter? GetDrawableCharacter(char c)
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
                
                var size = (w + _fontTexPad) * (h + _fontTexPad) * 4;
                var buffer = new byte[size];

                for (var ty = 0; ty < face.GlyphBitmap.rows; ty++)
                {
                    for (var tx = 0; tx < face.GlyphBitmap.width; tx++)
                    {
                        var px = Marshal.ReadByte(face.GlyphBitmap.buffer, (int) (ty * face.GlyphBitmap.width +
                            tx));
                        var idx = ty * w * 4 + tx * 4;
                        buffer[idx+0] = 255;
                        buffer[idx+1] = 255;
                        buffer[idx+2] = 255;
                        buffer[idx+3] = px;
                    }
                }

                GraphicsDevice.UpdateTexture(texture, buffer, _fontTexPad, _fontTexPad, 0, w,
                    h, 1, 0, 0);

                _characters.Add(c, new DrawableCharacter
                {
                    Texture = texture,
                    Size = new Vector2(w, h),
                    Bearing = new Vector2(face.GlyphBitmapLeft, face.GlyphBitmapTop),
                    Advance = face.Ascender / 64f * 5
                });
            }

            return _characters[c];
        }
        
        public void DrawText(string text, Color color, float fontSize, float x, float y)
        {
            if (_ft == null) return;
            
            var scale = fontSize / _fontSize;
            
            CommandList.PushDebugGroup("DrawText");
            foreach (var c in text)
            {
                var chn = GetDrawableCharacter(c);
                if (chn == null) continue;

                var ch = chn.Value;
                var xPos = x + (ch.Bearing.X - _fontTexPad) * scale;
                var yPos = y - (ch.Bearing.Y + _fontTexPad) * scale;
                var w = (ch.Size.X + _fontTexPad) * scale;
                var h = (ch.Size.Y + _fontTexPad) * scale;
                x += c == ' ' ? fontSize * 0.33f : w - _fontTexPad * scale + fontSize * 0.1f;

                if (c != ' ')
                {
                    DrawTexture(ch.Texture, xPos, yPos, w, h,
                        new TintInfo(new Vector3(color.R / 255f, color.G / 255f, color.B / 255f), 0, color.A / 255f));
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