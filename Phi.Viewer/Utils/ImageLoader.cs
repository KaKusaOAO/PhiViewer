using System;
using System.IO;
using BigGustave;
using Veldrid;

namespace Phi.Viewer.Utils
{
    public static class ImageLoader
    {
        private static ulong id = 0;
        
        private static Texture LoadTextureFromPng(Png png, string name = null)
        {
            var size = png.Width * png.Height * 4;
            var buffer = new byte[size];

            for (int y = 0; y < png.Height; y++)
            {
                for (int x = 0; x < png.Width; x++)
                {
                    var px = png.GetPixel(x, y);
                    var idx = (y * png.Width * 4) + x * 4;
                    buffer[idx+0] = px.R;
                    buffer[idx+1] = px.G;
                    buffer[idx+2] = px.B;
                    buffer[idx+3] = px.A;
                }
            }

            var renderer = PhiViewer.Instance.Renderer;
            var device = renderer.GraphicsDevice;
            var factory = renderer.Factory;

            var texture = factory.CreateTexture(TextureDescription.Texture2D((uint) png.Width, (uint) png.Height, 1, 1,
                PixelFormat.R8_G8_B8_A8_UNorm,
                TextureUsage.Sampled));
            texture.Name = name ?? $"ImageAsset-{id}";
            id += 1;
            factory.DisposeCollector.Remove(texture);
                            
            device.UpdateTexture(texture, buffer, 0, 0, 0, (uint)png.Width, (uint)png.Height, 1, 0, 0);
            return texture;
        }
        
        public static Texture LoadTextureFromStream(Stream stream, string name = null)
        {
            try
            {
                var png = Png.Open(stream);
                return LoadTextureFromPng(png, name);
            }
            catch (Exception)
            {
                return null;
            }
        }
        
        public static Texture LoadTextureFromPath(string path)
        {
            try
            {
                var png = Png.Open(path);
                return LoadTextureFromPng(png, path);
            } 
            catch (Exception)
            {
                return null;
            }
        }
    }
}