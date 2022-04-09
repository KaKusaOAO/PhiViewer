using System;
using System.Runtime.InteropServices;
using Phi.Viewer;
using Veldrid;
using Veldrid.Sdl2;

namespace Phi.Desktop
{
    internal class Sdl2DesktopWindow : IWindow
    {
        private Sdl2Window handle;
        
        [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
        private unsafe delegate void GetDrawableSizeDelegate(SDL_Window window, int* x, int* y);

        private static GetDrawableSizeDelegate s_GL_GetDrawableSize =>
            Sdl2Native.LoadFunction<GetDrawableSizeDelegate>("SDL_GL_GetDrawableSize");

        public GraphicsDevice GraphicsDevice { get; }

        public bool Exists => handle.Exists;

        public float Width => GetWindowWidth();

        public float Height => GetWindowHeight();

        private unsafe float GetWindowWidth()
        {
            int x, y;
            Sdl2Native.SDL_GetWindowSize(handle.SdlWindowHandle, &x, &y);
            // s_GL_GetDrawableSize(handle.SdlWindowHandle, &x, &y);
            return x;
        }
    
        private unsafe float GetWindowHeight()
        {
            int x, y;
            Sdl2Native.SDL_GetWindowSize(handle.SdlWindowHandle, &x, &y);
            // s_GL_GetDrawableSize(handle.SdlWindowHandle, &x, &y);
            return y;
        }
        
        public event Action WindowResized;

        public Sdl2DesktopWindow(Sdl2Window window, GraphicsDevice device)
        {
            handle = window;
            GraphicsDevice = device;

            handle.Resized += () => WindowResized?.Invoke();
        }

        public InputSnapshot PumpEvents()
        {
            return handle.PumpEvents();
        }
    }
}