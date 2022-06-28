using System.Collections.Generic;
using System.Linq;
using System.Numerics;

namespace Phi.Viewer.Graphics
{
    public class Mesh
    {
        public List<Vector3> Vertices { get; private set; } = new List<Vector3>();
        public List<Vector2> UVs { get; private set; } = new List<Vector2>();
        public List<ushort> Indices { get; private set; } = new List<ushort>();

        public static Mesh CreateQuad(float x, float y, float width, float height, Vector2[] uv = null)
        {
            var quadMesh = new List<Vector3>
            {
                new Vector3(x, y, 0),
                new Vector3(x + width, y, 0),
                new Vector3(x, y + height, 0),
                new Vector3(x + width, y + height, 0)
            };
            
            var quadUv = uv?.ToList() ?? new List<Vector2>()
            {
                new Vector2(0, 0),
                new Vector2(1, 0),
                new Vector2(0, 1),
                new Vector2(1, 1)
            };
            
            var quadIndices = new List<ushort> { 0, 1, 2, 2, 1, 3 };

            return new Mesh
            {
                Vertices = quadMesh,
                UVs = quadUv,
                Indices = quadIndices
            };
        }
    }
}