using System;
using System.Collections.Generic;
using System.Numerics;
using ImGuiNET;
using Veldrid;

namespace Phi.Viewer.Graphics
{
    public class BetterImGuiRenderer : ImGuiRenderer
    {
        public BetterImGuiRenderer(GraphicsDevice gd, OutputDescription outputDescription, int width, int height) : base(gd, outputDescription, width, height)
        {
            InitImGui();
        }

        public BetterImGuiRenderer(GraphicsDevice gd, OutputDescription outputDescription, int width, int height, ColorSpaceHandling colorSpaceHandling) : base(gd, outputDescription, width, height, colorSpaceHandling)
        {
            InitImGui();
        }

        private void InitImGui()
        {
            ImGui.StyleColorsClassic();

            var io = ImGui.GetIO();
            io.BackendFlags |= ImGuiBackendFlags.HasGamepad | ImGuiBackendFlags.HasSetMousePos | ImGuiBackendFlags.HasMouseCursors;
            io.ConfigFlags |= ImGuiConfigFlags.DockingEnable;

            var style = ImGui.GetStyle();
            style.WindowPadding = new Vector2(10, 7);
            style.FramePadding = new Vector2(13, 6);
            style.ScrollbarSize = 10;
            style.FrameRounding = style.GrabRounding = style.WindowRounding = 12;
            // style.WindowTitleAlign = new Vector2(0.5f, 0.5f);
            style.Colors[15] = new Vector4(197 / 255f, 197 / 255f, 197 / 255f, 77 / 255f);
        }

        public void EnhancedUpdate(float deltaSeconds, InputSnapshot snapshot)
        {
            BeginUpdate(deltaSeconds);
            UpdateImGuiInput(snapshot);
            EndUpdate();
        }

        private bool _controlDown;
        private bool _shiftDown;
        private bool _altDown;
    
        private void UpdateImGuiInput(InputSnapshot snapshot)
        {
            ImGuiIOPtr io = ImGui.GetIO();

            // Determine if any of the mouse buttons were pressed during this snapshot period, even if they are no longer held.
            bool leftPressed = false;
            bool middlePressed = false;
            bool rightPressed = false;
            for (int i = 0; i < snapshot.MouseEvents.Count; i++)
            {
                MouseEvent me = snapshot.MouseEvents[i];
                if (me.Down)
                {
                    switch (me.MouseButton)
                    {
                        case MouseButton.Left:
                            leftPressed = true;
                            break;
                        case MouseButton.Middle:
                            middlePressed = true;
                            break;
                        case MouseButton.Right:
                            rightPressed = true;
                            break;
                    }
                }
            }

            io.MouseDown[0] = leftPressed || snapshot.IsMouseDown(MouseButton.Left);
            io.MouseDown[1] = rightPressed || snapshot.IsMouseDown(MouseButton.Right);
            io.MouseDown[2] = middlePressed || snapshot.IsMouseDown(MouseButton.Middle);
            io.MousePos = snapshot.MousePosition;
            io.MouseWheel = snapshot.WheelDelta;

            IReadOnlyList<char> keyCharPresses = snapshot.KeyCharPresses;
            for (int i = 0; i < keyCharPresses.Count; i++)
            {
                char c = keyCharPresses[i];
                ImGui.GetIO().AddInputCharacter(c);
            }

            IReadOnlyList<KeyEvent> keyEvents = snapshot.KeyEvents;
            for (int i = 0; i < keyEvents.Count; i++)
            {
                KeyEvent keyEvent = keyEvents[i];
                var key = GetImGuiKey(keyEvent.Key);
                io.AddKeyEvent(key, keyEvent.Down);
                if (keyEvent.Key == Key.ControlLeft)
                {
                    _controlDown = keyEvent.Down;
                }
                if (keyEvent.Key == Key.ShiftLeft)
                {
                    _shiftDown = keyEvent.Down;
                }
                if (keyEvent.Key == Key.AltLeft)
                {
                    _altDown = keyEvent.Down;
                }
            }

            io.KeyCtrl = _controlDown;
            io.KeyAlt = _altDown;
            io.KeyShift = _shiftDown;
        }

        private ImGuiKey GetImGuiKey(Key key)
        {
            return key switch
            {
                Key.Unknown => ImGuiKey.None,
                Key.ShiftLeft => ImGuiKey.LeftShift,
                Key.ShiftRight => ImGuiKey.RightShift,
                Key.ControlLeft => ImGuiKey.LeftCtrl,
                Key.ControlRight => ImGuiKey.RightCtrl,
                Key.AltLeft => ImGuiKey.LeftAlt,
                Key.AltRight => ImGuiKey.RightAlt,
                Key.WinLeft => ImGuiKey.LeftSuper,
                Key.WinRight => ImGuiKey.RightSuper,
                Key.Menu => ImGuiKey.Menu,
                Key.F1 => ImGuiKey.F1,
                Key.F2 => ImGuiKey.F2,
                Key.F3 => ImGuiKey.F3,
                Key.F4 => ImGuiKey.F4,
                Key.F5 => ImGuiKey.F5,
                Key.F6 => ImGuiKey.F6,
                Key.F7 => ImGuiKey.F7,
                Key.F8 => ImGuiKey.F8,
                Key.F9 => ImGuiKey.F9,
                Key.F10 => ImGuiKey.F10,
                Key.F11 => ImGuiKey.F11,
                Key.F12 => ImGuiKey.F12,
                Key.F13 => ImGuiKey.None,
                Key.F14 => ImGuiKey.None,
                Key.F15 => ImGuiKey.None,
                Key.F16 => ImGuiKey.None,
                Key.F17 => ImGuiKey.None,
                Key.F18 => ImGuiKey.None,
                Key.F19 => ImGuiKey.None,
                Key.F20 => ImGuiKey.None,
                Key.F21 => ImGuiKey.None,
                Key.F22 => ImGuiKey.None,
                Key.F23 => ImGuiKey.None,
                Key.F24 => ImGuiKey.None,
                Key.F25 => ImGuiKey.None,
                Key.F26 => ImGuiKey.None,
                Key.F27 => ImGuiKey.None,
                Key.F28 => ImGuiKey.None,
                Key.F29 => ImGuiKey.None,
                Key.F30 => ImGuiKey.None,
                Key.F31 => ImGuiKey.None,
                Key.F32 => ImGuiKey.None,
                Key.F33 => ImGuiKey.None,
                Key.F34 => ImGuiKey.None,
                Key.F35 => ImGuiKey.None,
                Key.Up => ImGuiKey.UpArrow,
                Key.Down => ImGuiKey.DownArrow,
                Key.Left => ImGuiKey.LeftArrow,
                Key.Right => ImGuiKey.RightArrow,
                Key.Enter => ImGuiKey.Enter,
                Key.Escape => ImGuiKey.Escape,
                Key.Space => ImGuiKey.Space,
                Key.Tab => ImGuiKey.Tab,
                Key.BackSpace => ImGuiKey.Backspace,
                Key.Insert => ImGuiKey.Insert,
                Key.Delete => ImGuiKey.Delete,
                Key.PageUp => ImGuiKey.PageUp,
                Key.PageDown => ImGuiKey.PageDown,
                Key.Home => ImGuiKey.Home,
                Key.End => ImGuiKey.End,
                Key.CapsLock => ImGuiKey.CapsLock,
                Key.ScrollLock => ImGuiKey.ScrollLock,
                Key.PrintScreen => ImGuiKey.PrintScreen,
                Key.Pause => ImGuiKey.Pause,
                Key.NumLock => ImGuiKey.NumLock,
                Key.Clear => ImGuiKey.None,
                Key.Sleep => ImGuiKey.None,
                Key.Keypad0 => ImGuiKey.Keypad0,
                Key.Keypad1 => ImGuiKey.Keypad1,
                Key.Keypad2 => ImGuiKey.Keypad2,
                Key.Keypad3 => ImGuiKey.Keypad3,
                Key.Keypad4 => ImGuiKey.Keypad4,
                Key.Keypad5 => ImGuiKey.Keypad5,
                Key.Keypad6 => ImGuiKey.Keypad6,
                Key.Keypad7 => ImGuiKey.Keypad7,
                Key.Keypad8 => ImGuiKey.Keypad8,
                Key.Keypad9 => ImGuiKey.Keypad9,
                Key.KeypadDivide => ImGuiKey.KeypadDivide,
                Key.KeypadMultiply => ImGuiKey.KeypadMultiply,
                Key.KeypadSubtract => ImGuiKey.KeypadSubtract,
                Key.KeypadAdd => ImGuiKey.KeypadAdd,
                Key.KeypadDecimal => ImGuiKey.KeypadDecimal,
                Key.KeypadEnter => ImGuiKey.KeypadEnter,
                Key.A => ImGuiKey.A,
                Key.B => ImGuiKey.B,
                Key.C => ImGuiKey.C,
                Key.D => ImGuiKey.D,
                Key.E => ImGuiKey.E,
                Key.F => ImGuiKey.F,
                Key.G => ImGuiKey.G,
                Key.H => ImGuiKey.H,
                Key.I => ImGuiKey.I,
                Key.J => ImGuiKey.J,
                Key.K => ImGuiKey.K,
                Key.L => ImGuiKey.L,
                Key.M => ImGuiKey.M,
                Key.N => ImGuiKey.N,
                Key.O => ImGuiKey.O,
                Key.P => ImGuiKey.P,
                Key.Q => ImGuiKey.Q,
                Key.R => ImGuiKey.R,
                Key.S => ImGuiKey.S,
                Key.T => ImGuiKey.T,
                Key.U => ImGuiKey.U,
                Key.V => ImGuiKey.V,
                Key.W => ImGuiKey.W,
                Key.X => ImGuiKey.X,
                Key.Y => ImGuiKey.Y,
                Key.Z => ImGuiKey.Z,
                Key.Number0 => ImGuiKey._0,
                Key.Number1 => ImGuiKey._1,
                Key.Number2 => ImGuiKey._2,
                Key.Number3 => ImGuiKey._3,
                Key.Number4 => ImGuiKey._4,
                Key.Number5 => ImGuiKey._5,
                Key.Number6 => ImGuiKey._6,
                Key.Number7 => ImGuiKey._7,
                Key.Number8 => ImGuiKey._8,
                Key.Number9 => ImGuiKey._9,
                Key.Tilde => ImGuiKey.GraveAccent,
                Key.Minus => ImGuiKey.Minus,
                Key.Plus => ImGuiKey.Equal,
                Key.BracketLeft => ImGuiKey.LeftBracket,
                Key.BracketRight => ImGuiKey.RightBracket,
                Key.Semicolon => ImGuiKey.Semicolon,
                Key.Quote => ImGuiKey.Apostrophe,
                Key.Comma => ImGuiKey.Comma,
                Key.Period => ImGuiKey.Period,
                Key.Slash => ImGuiKey.Slash,
                Key.BackSlash => ImGuiKey.Backslash,
                Key.NonUSBackSlash => ImGuiKey.Backslash,
                Key.LastKey => ImGuiKey.None,
                _ => throw new ArgumentOutOfRangeException(nameof(key), key, null)
            };
        }
    }
}