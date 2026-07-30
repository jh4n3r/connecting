using System;
using System.Runtime.InteropServices;
using System.Windows.Forms;

namespace Conecting.Core
{
    /// <summary>
    /// High-Precision Native Win32 Input Injector.
    /// Uses official Win32 SendInput API for UIPI and Admin elevation compliance.
    /// </summary>
    public static class NativeInputInjector
    {
        [DllImport("user32.dll", SetLastError = true)]
        public static extern uint SendInput(uint nInputs, INPUT[] pInputs, int cbSize);

        [DllImport("user32.dll")]
        public static extern bool SetCursorPos(int X, int Y);

        [DllImport("user32.dll")]
        public static extern uint MapVirtualKey(uint uCode, uint uMapType);

        [StructLayout(LayoutKind.Sequential)]
        public struct INPUT
        {
            public uint type;
            public InputUnion U;
        }

        [StructLayout(LayoutKind.Explicit)]
        public struct InputUnion
        {
            [FieldOffset(0)]
            public MOUSEINPUT mi;
            [FieldOffset(0)]
            public KEYBDINPUT ki;
        }

        [StructLayout(LayoutKind.Sequential)]
        public struct MOUSEINPUT
        {
            public int dx;
            public int dy;
            public uint mouseData;
            public uint dwFlags;
            public uint time;
            public IntPtr dwExtraInfo;
        }

        [StructLayout(LayoutKind.Sequential)]
        public struct KEYBDINPUT
        {
            public ushort wVk;
            public ushort wScan;
            public uint dwFlags;
            public uint time;
            public IntPtr dwExtraInfo;
        }

        public const uint INPUT_MOUSE = 0;
        public const uint INPUT_KEYBOARD = 1;

        public const uint MOUSEEVENTF_MOVE = 0x0001;
        public const uint MOUSEEVENTF_LEFTDOWN = 0x0002;
        public const uint MOUSEEVENTF_LEFTUP = 0x0004;
        public const uint MOUSEEVENTF_RIGHTDOWN = 0x0008;
        public const uint MOUSEEVENTF_RIGHTUP = 0x0010;
        public const uint MOUSEEVENTF_WHEEL = 0x0800;
        public const uint MOUSEEVENTF_ABSOLUTE = 0x8000;
        public const uint MOUSEEVENTF_VIRTUALDESK = 0x4000;

        public const uint KEYEVENTF_EXTENDEDKEY = 0x0001;
        public const uint KEYEVENTF_KEYUP = 0x0002;
        public const uint KEYEVENTF_SCANCODE = 0x0004;

        /// <summary>
        /// Executes mouse movement, click, drag, or scroll using Win32 SendInput.
        /// </summary>
        public static void ExecuteMouseInput(byte eventType, float normalizedX, float normalizedY)
        {
            try
            {
                if (eventType == 0x06) // Mouse Wheel Up
                {
                    InjectMouseWheel(120);
                    return;
                }
                else if (eventType == 0x07) // Mouse Wheel Down
                {
                    InjectMouseWheel(-120);
                    return;
                }

                int screenWidth = Screen.PrimaryScreen.Bounds.Width;
                int screenHeight = Screen.PrimaryScreen.Bounds.Height;
                int targetPixelX = (int)(normalizedX * screenWidth);
                int targetPixelY = (int)(normalizedY * screenHeight);

                SetCursorPos(targetPixelX, targetPixelY);

                uint absoluteX = (uint)(normalizedX * 65535.0f);
                uint absoluteY = (uint)(normalizedY * 65535.0f);

                uint flags = MOUSEEVENTF_ABSOLUTE | MOUSEEVENTF_VIRTUALDESK | MOUSEEVENTF_MOVE;

                if (eventType == 0x02) // Left Button Down
                {
                    flags |= MOUSEEVENTF_LEFTDOWN;
                }
                else if (eventType == 0x03) // Left Button Up
                {
                    flags |= MOUSEEVENTF_LEFTUP;
                }
                else if (eventType == 0x04) // Right Button Down
                {
                    flags |= MOUSEEVENTF_RIGHTDOWN;
                }
                else if (eventType == 0x05) // Right Button Up
                {
                    flags |= MOUSEEVENTF_RIGHTUP;
                }
                else if (eventType == 0x01) // Move / Drag
                {
                    // For movement, only MOUSEEVENTF_ABSOLUTE | MOUSEEVENTF_VIRTUALDESK | MOUSEEVENTF_MOVE is required.
                }

                INPUT[] inputs = new INPUT[1];
                inputs[0].type = INPUT_MOUSE;
                inputs[0].U.mi.dx = (int)absoluteX;
                inputs[0].U.mi.dy = (int)absoluteY;
                inputs[0].U.mi.dwFlags = flags;
                inputs[0].U.mi.mouseData = 0;
                inputs[0].U.mi.time = 0;
                inputs[0].U.mi.dwExtraInfo = IntPtr.Zero;

                SendInput(1, inputs, Marshal.SizeOf(typeof(INPUT)));
            }
            catch { }
        }

        private static void InjectMouseWheel(int scrollDelta)
        {
            try
            {
                INPUT[] inputs = new INPUT[1];
                inputs[0].type = INPUT_MOUSE;
                inputs[0].U.mi.dx = 0;
                inputs[0].U.mi.dy = 0;
                inputs[0].U.mi.dwFlags = MOUSEEVENTF_WHEEL;
                inputs[0].U.mi.mouseData = unchecked((uint)scrollDelta);
                inputs[0].U.mi.time = 0;
                inputs[0].U.mi.dwExtraInfo = IntPtr.Zero;

                SendInput(1, inputs, Marshal.SizeOf(typeof(INPUT)));
            }
            catch { }
        }

        /// <summary>
        /// Executes keyboard press or release using Win32 SendInput.
        /// </summary>
        public static void ExecuteKeyboardInput(byte virtualKeyCode, bool isKeyDown)
        {
            try
            {
                ushort scanCode = (ushort)MapVirtualKey(virtualKeyCode, 0);
                uint flags = isKeyDown ? 0u : KEYEVENTF_KEYUP;

                if ((virtualKeyCode >= 0x21 && virtualKeyCode <= 0x28) || 
                    virtualKeyCode == 0x2C || virtualKeyCode == 0x2D || virtualKeyCode == 0x2E || 
                    virtualKeyCode == 0x5B || virtualKeyCode == 0x5C || virtualKeyCode == 0xA3 || virtualKeyCode == 0xA5)
                {
                    flags |= KEYEVENTF_EXTENDEDKEY;
                }

                INPUT[] inputs = new INPUT[1];
                inputs[0].type = INPUT_KEYBOARD;
                inputs[0].U.ki.wVk = virtualKeyCode;
                inputs[0].U.ki.wScan = scanCode;
                inputs[0].U.ki.dwFlags = flags;
                inputs[0].U.ki.time = 0;
                inputs[0].U.ki.dwExtraInfo = IntPtr.Zero;

                SendInput(1, inputs, Marshal.SizeOf(typeof(INPUT)));
            }
            catch { }
        }
    }
}
