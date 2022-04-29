using System;
using KaLib.Utils;
using Veldrid;

namespace Phi.Viewer.Graphics
{
    public class MeshRenderer : IDisposable
    {
        public Mesh Mesh { get; set; }
        private DeviceBuffer _vertexBuffer;
        private DeviceBuffer _indexBuffer;
        public Action<DeviceBuffer, DeviceBuffer> OnBuffersCreated;
        public bool NeedsUpdate { get; set; } = true;

        public void SetBuffers(DeviceBuffer vertexBuffer, DeviceBuffer indexBuffer)
        {
            _vertexBuffer = vertexBuffer;
            _indexBuffer = indexBuffer;
        }
        
        public void UpdateBuffer(Renderer renderer)
        {
            if (Mesh == null) return;
            _vertexBuffer?.Dispose();
            _vertexBuffer = null;
            _indexBuffer?.Dispose();
            _indexBuffer = null;

            var mesh = Mesh.Vertices.ToArray();
            var uv = Mesh.UVs.ToArray();
            if (mesh.Length != uv.Length)
            {
                throw new ArgumentException("UV length doesn't match the mesh length");
            }
            
            var vertices = new VertexInfo[mesh.Length];
            for (var i = 0; i < mesh.Length; i++)
            {
                vertices[i] = new VertexInfo
                {
                    Position = mesh[i],
                    Uv = uv[i]
                };
            }

            var gd = renderer.GraphicsDevice;
            var factory = renderer.Factory;
            
            var vbDescription = new BufferDescription((uint)(20 * mesh.Length), BufferUsage.VertexBuffer);
            var vertexBuffer = factory.CreateBuffer(vbDescription);
            vertexBuffer.Name = "Vertex Buffer";
            gd.UpdateBuffer(vertexBuffer, 0, vertices);

            var indices = Mesh.Indices.ToArray();
            var ibDescription = new BufferDescription(
                (uint)indices.Length * sizeof(ushort),
                BufferUsage.IndexBuffer);
            var indexBuffer = factory.CreateBuffer(ibDescription);
            indexBuffer.Name = "Index Buffer";
            gd.UpdateBuffer(indexBuffer, 0, indices);
            
            OnBuffersCreated?.Invoke(vertexBuffer, indexBuffer);
            SetBuffers(vertexBuffer, indexBuffer);
        }

        public void Render(Renderer renderer)
        {
            if (_vertexBuffer == null || _indexBuffer == null || NeedsUpdate)
            {
                NeedsUpdate = false;
                UpdateBuffer(renderer);
            }

            if (_vertexBuffer != null && _indexBuffer != null)
            {
                renderer.DrawBuffers(_vertexBuffer, _indexBuffer);
            }
            else
            {
                Logger.Warn("Vertex or index buffer is null!");
            }
        }
        
        public void Dispose()
        {
            _vertexBuffer?.Dispose();
            _indexBuffer?.Dispose();
        }
    }
}