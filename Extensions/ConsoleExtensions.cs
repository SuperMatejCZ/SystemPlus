﻿using System;
using System.Runtime.InteropServices;

namespace SystemPlus.Extensions
{
    public static class ConsoleExtensions
    {
        private const int FixedWidthTrueType = 54;
        private const int StandardOutputHandle = -11;

        [DllImport("kernel32.dll", SetLastError = true)]
        internal static extern IntPtr GetStdHandle(int nStdHandle);

        [return: MarshalAs(UnmanagedType.Bool)]
        [DllImport("kernel32.dll", SetLastError = true, CharSet = CharSet.Unicode)]
        internal static extern bool SetCurrentConsoleFontEx(IntPtr hConsoleOutput, bool MaximumWindow, ref FontInfo ConsoleCurrentFontEx);

        [return: MarshalAs(UnmanagedType.Bool)]
        [DllImport("kernel32.dll", SetLastError = true, CharSet = CharSet.Unicode)]
        internal static extern bool GetCurrentConsoleFontEx(IntPtr hConsoleOutput, bool MaximumWindow, ref FontInfo ConsoleCurrentFontEx);


        private static readonly IntPtr ConsoleOutputHandle = GetStdHandle(StandardOutputHandle);

        [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
        public struct FontInfo
        {
            internal int cbSize;
            internal int FontIndex;
            internal short FontWidth;
            public short FontSize;
            public int FontFamily;
            public int FontWeight;
            [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 32)]
            public string FontName;
        }

        public static void SetFontSize(short size)
        {
            size = (short)MathPlus.Clamp(size, (short)0, (short)1000);
            SetCurrentFont(currentFont, size);
        }

        private static string currentFont = "Consolas";

        public static FontInfo[] SetCurrentFont(string font, short fontSize = 0)
        {
            FontInfo before = new FontInfo
            {
                cbSize = Marshal.SizeOf<FontInfo>()
            };

            if (GetCurrentConsoleFontEx(ConsoleOutputHandle, false, ref before))
            {

                FontInfo set = new FontInfo
                {
                    cbSize = Marshal.SizeOf<FontInfo>(),
                    FontIndex = 0,
                    FontFamily = FixedWidthTrueType,
                    FontName = font,
                    FontWeight = 400,
                    FontSize = fontSize > 0 ? fontSize : before.FontSize
                };

                // Get some settings from current font.
                if (!SetCurrentConsoleFontEx(ConsoleOutputHandle, false, ref set))
                {
                    var ex = Marshal.GetLastWin32Error();
                    Console.WriteLine("Set error " + ex);
                    throw new System.ComponentModel.Win32Exception(ex);
                }

                FontInfo after = new FontInfo
                {
                    cbSize = Marshal.SizeOf<FontInfo>()
                };
                GetCurrentConsoleFontEx(ConsoleOutputHandle, false, ref after);
                currentFont = font;
                return new[] { before, set, after };
            }
            else
            {
                var er = Marshal.GetLastWin32Error();
                Console.WriteLine("Get error " + er);
                throw new System.ComponentModel.Win32Exception(er);
            }
        }

        [DllImport("kernel32.dll", ExactSpelling = true)]
        private static extern IntPtr GetConsoleWindow();
        private static IntPtr ThisConsole = GetConsoleWindow();
        [DllImport("user32.dll", CharSet = CharSet.Auto, SetLastError = true)]
        private static extern bool ShowWindow(IntPtr hWnd, int nCmdShow);
        private const int HIDE = 0;
        private const int MAXIMIZE = 3;
        private const int MINIMIZE = 6;
        private const int RESTORE = 9;

        public static void Maximize()
        {
            Console.SetWindowSize(Console.LargestWindowWidth, Console.LargestWindowHeight);
            ShowWindow(ThisConsole, MAXIMIZE);
        }

        public static void Write(string value, bool highlight)
        {
            if (highlight)
            {
                Console.ForegroundColor = ConsoleColor.Black;
                Console.BackgroundColor = ConsoleColor.Gray;
            }
            Console.Write(value);
            if (highlight)
                Console.ResetColor();
        }
        public static void Write(object value, bool highlight) => Write(value.ToString(), highlight);

        public static void WriteLine(string value, bool highlight) => Write(value + "\n", highlight);
        public static void WriteLine(object value, bool highlight) => WriteLine(value.ToString(), highlight);

        public static string ReadLine(string display)
        {
            int left = Console.CursorLeft;
            string writen = "";
            int pos = 0;

            while (true)
            {
                Console.CursorLeft = left;
                if (writen == "")
                {
                    Console.ForegroundColor = ConsoleColor.Gray;
                    Console.Write(display);
                    Console.ResetColor();
                }
                else
                    Console.Write(writen);

                ConsoleKeyInfo info = Console.ReadKey(true);

                if (info.Key == ConsoleKey.Enter)
                    return writen;
                else if (info.Key == ConsoleKey.Backspace && pos > 0)
                {
                    writen = writen.Substring(0, pos - 1) + writen.Substring(pos, writen.Length - pos);
                    pos--;
                }
                else if (info.Key == ConsoleKey.LeftArrow && pos > 0)
                    pos--;
                else if (info.Key == ConsoleKey.RightArrow && pos < writen.Length)
                    pos++;
            }
        }

        // Set Console Window Position
        private const int SWP_NOZORDER = 0x4;
        private const int SWP_NOACTIVATE = 0x10;

        [DllImport("user32")]
        private static extern bool SetWindowPos(IntPtr hWnd, IntPtr hWndInsertAfter,
            int x, int y, int cx, int cy, int flags);


        /// <summary>
        /// Sets the console window location and size in pixels
        /// </summary>
        public static void SetWindowPosition(int x, int y, int width, int height)
        {
            SetWindowPos(ConsoleHandle, IntPtr.Zero, x, y, width, height, SWP_NOZORDER | SWP_NOACTIVATE);
        }

        public static IntPtr ConsoleHandle
        {
            get
            {
                return GetConsoleWindow();
            }
        }
    }
}
