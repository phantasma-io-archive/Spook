using System;
using System.Drawing;
using System.Runtime.InteropServices;
using System.Collections.Generic;

namespace Phantasma.Spook
{
    /// <summary>
    /// A Win32 COLORREF, used to specify an RGB color.  See MSDN for more information:
    /// https://msdn.microsoft.com/en-us/library/windows/desktop/dd183449(v=vs.85).aspx
    /// </summary>
    [StructLayout(LayoutKind.Sequential)]
    public struct COLORREF
    {
        private uint ColorDWORD;

        internal COLORREF(Color color)
        {
            ColorDWORD = (uint)color.R + (((uint)color.G) << 8) + (((uint)color.B) << 16);
        }

        internal COLORREF(uint r, uint g, uint b)
        {
            ColorDWORD = r + (g << 8) + (b << 16);
        }

        public override string ToString()
        {
            return ColorDWORD.ToString();
        }
    }

    /// <summary>
    /// Exposes methods used for mapping System.Drawing.Colors to System.ConsoleColors.
    /// Based on code that was originally written by Alex Shvedov, and that was then modified by MercuryP.
    /// </summary>
    public static class ColorMapper
    {
        [StructLayout(LayoutKind.Sequential)]
        private struct COORD
        {
            internal short X;
            internal short Y;
        }

        [StructLayout(LayoutKind.Sequential)]
        private struct SMALL_RECT
        {
            internal short Left;
            internal short Top;
            internal short Right;
            internal short Bottom;
        }

        [StructLayout(LayoutKind.Sequential)]
        private struct CONSOLE_SCREEN_BUFFER_INFO_EX
        {
            internal int cbSize;
            internal COORD dwSize;
            internal COORD dwCursorPosition;
            internal ushort wAttributes;
            internal SMALL_RECT srWindow;
            internal COORD dwMaximumWindowSize;
            internal ushort wPopupAttributes;
            internal bool bFullscreenSupported;
            internal COLORREF black;
            internal COLORREF darkBlue;
            internal COLORREF darkGreen;
            internal COLORREF darkCyan;
            internal COLORREF darkRed;
            internal COLORREF darkMagenta;
            internal COLORREF darkYellow;
            internal COLORREF gray;
            internal COLORREF darkGray;
            internal COLORREF blue;
            internal COLORREF green;
            internal COLORREF cyan;
            internal COLORREF red;
            internal COLORREF magenta;
            internal COLORREF yellow;
            internal COLORREF white;
        }

        private const int STD_OUTPUT_HANDLE = -11;                               // per WinBase.h
        private static readonly IntPtr INVALID_HANDLE_VALUE = new IntPtr(-1);    // per WinBase.h

        [DllImport("kernel32.dll", SetLastError = true)]
        private static extern IntPtr GetStdHandle(int nStdHandle);

        [DllImport("kernel32.dll", SetLastError = true)]
        private static extern bool GetConsoleScreenBufferInfoEx(IntPtr hConsoleOutput, ref CONSOLE_SCREEN_BUFFER_INFO_EX csbe);

        [DllImport("kernel32.dll", SetLastError = true)]
        private static extern bool SetConsoleScreenBufferInfoEx(IntPtr hConsoleOutput, ref CONSOLE_SCREEN_BUFFER_INFO_EX csbe);

        /// <summary>
        /// Maps a System.Drawing.Color to a System.ConsoleColor.
        /// </summary>
        /// <param name="oldColor">The color to be replaced.</param>
        /// <param name="newColor">The color to be mapped.</param>
        public static void MapColor(ConsoleColor oldColor, Color newColor)
        {
            // NOTE: The default console colors used are gray (foreground) and black (background).
            MapColor(oldColor, newColor.R, newColor.G, newColor.B);
        }

        /// <summary>
        /// Gets a collection of all 16 colors in the console buffer.
        /// </summary>
        /// <returns>Returns all 16 COLORREFs in the console buffer as a dictionary keyed by the COLORREF's alias
        /// in the buffer's ColorTable.</returns>
        public static Dictionary<ConsoleColor, COLORREF> GetBufferColors()
        {
            var colors = new Dictionary<ConsoleColor, COLORREF>();
            IntPtr hConsoleOutput = GetStdHandle(STD_OUTPUT_HANDLE);    // 7
            CONSOLE_SCREEN_BUFFER_INFO_EX csbe = GetBufferInfo(hConsoleOutput);

            colors.Add(ConsoleColor.Black, csbe.black);
            colors.Add(ConsoleColor.DarkBlue, csbe.darkBlue);
            colors.Add(ConsoleColor.DarkGreen, csbe.darkGreen);
            colors.Add(ConsoleColor.DarkCyan, csbe.darkCyan);
            colors.Add(ConsoleColor.DarkRed, csbe.darkRed);
            colors.Add(ConsoleColor.DarkMagenta, csbe.darkMagenta);
            colors.Add(ConsoleColor.DarkYellow, csbe.darkYellow);
            colors.Add(ConsoleColor.Gray, csbe.gray);
            colors.Add(ConsoleColor.DarkGray, csbe.darkGray);
            colors.Add(ConsoleColor.Blue, csbe.blue);
            colors.Add(ConsoleColor.Green, csbe.green);
            colors.Add(ConsoleColor.Cyan, csbe.cyan);
            colors.Add(ConsoleColor.Red, csbe.red);
            colors.Add(ConsoleColor.Magenta, csbe.magenta);
            colors.Add(ConsoleColor.Yellow, csbe.yellow);
            colors.Add(ConsoleColor.White, csbe.white);

            return colors;
        }

        /// <summary>
        /// Sets all 16 colors in the console buffer using colors supplied in a dictionary.
        /// </summary>
        /// <param name="colors">A dictionary containing COLORREFs keyed by the COLORREF's alias in the buffer's 
        /// ColorTable.</param>
        public static void SetBatchBufferColors(Dictionary<ConsoleColor, COLORREF> colors)
        {
            IntPtr hConsoleOutput = GetStdHandle(STD_OUTPUT_HANDLE); // 7
            CONSOLE_SCREEN_BUFFER_INFO_EX csbe = GetBufferInfo(hConsoleOutput);

            csbe.black = colors[ConsoleColor.Black];
            csbe.darkBlue = colors[ConsoleColor.DarkBlue];
            csbe.darkGreen = colors[ConsoleColor.DarkGreen];
            csbe.darkCyan = colors[ConsoleColor.DarkCyan];
            csbe.darkRed = colors[ConsoleColor.DarkRed];
            csbe.darkMagenta = colors[ConsoleColor.DarkMagenta];
            csbe.darkYellow = colors[ConsoleColor.DarkYellow];
            csbe.gray = colors[ConsoleColor.Gray];
            csbe.darkGray = colors[ConsoleColor.DarkGray];
            csbe.blue = colors[ConsoleColor.Blue];
            csbe.green = colors[ConsoleColor.Green];
            csbe.cyan = colors[ConsoleColor.Cyan];
            csbe.red = colors[ConsoleColor.Red];
            csbe.magenta = colors[ConsoleColor.Magenta];
            csbe.yellow = colors[ConsoleColor.Yellow];
            csbe.white = colors[ConsoleColor.White];

            SetBufferInfo(hConsoleOutput, csbe);
        }

        private static CONSOLE_SCREEN_BUFFER_INFO_EX GetBufferInfo(IntPtr hConsoleOutput)
        {
            CONSOLE_SCREEN_BUFFER_INFO_EX csbe = new CONSOLE_SCREEN_BUFFER_INFO_EX();
            csbe.cbSize = (int)Marshal.SizeOf(csbe); // 96 = 0x60

            if (hConsoleOutput == INVALID_HANDLE_VALUE)
            {
                throw CreateException(Marshal.GetLastWin32Error());
            }

            bool brc = GetConsoleScreenBufferInfoEx(hConsoleOutput, ref csbe);

            if (!brc)
            {
                throw CreateException(Marshal.GetLastWin32Error());
            }

            return csbe;
        }

        private static void MapColor(ConsoleColor color, uint r, uint g, uint b)
        {
            IntPtr hConsoleOutput = GetStdHandle(STD_OUTPUT_HANDLE); // 7
            CONSOLE_SCREEN_BUFFER_INFO_EX csbe = GetBufferInfo(hConsoleOutput);

            switch (color)
            {
                case ConsoleColor.Black:
                    csbe.black = new COLORREF(r, g, b);
                    break;
                case ConsoleColor.DarkBlue:
                    csbe.darkBlue = new COLORREF(r, g, b);
                    break;
                case ConsoleColor.DarkGreen:
                    csbe.darkGreen = new COLORREF(r, g, b);
                    break;
                case ConsoleColor.DarkCyan:
                    csbe.darkCyan = new COLORREF(r, g, b);
                    break;
                case ConsoleColor.DarkRed:
                    csbe.darkRed = new COLORREF(r, g, b);
                    break;
                case ConsoleColor.DarkMagenta:
                    csbe.darkMagenta = new COLORREF(r, g, b);
                    break;
                case ConsoleColor.DarkYellow:
                    csbe.darkYellow = new COLORREF(r, g, b);
                    break;
                case ConsoleColor.Gray:
                    csbe.gray = new COLORREF(r, g, b);
                    break;
                case ConsoleColor.DarkGray:
                    csbe.darkGray = new COLORREF(r, g, b);
                    break;
                case ConsoleColor.Blue:
                    csbe.blue = new COLORREF(r, g, b);
                    break;
                case ConsoleColor.Green:
                    csbe.green = new COLORREF(r, g, b);
                    break;
                case ConsoleColor.Cyan:
                    csbe.cyan = new COLORREF(r, g, b);
                    break;
                case ConsoleColor.Red:
                    csbe.red = new COLORREF(r, g, b);
                    break;
                case ConsoleColor.Magenta:
                    csbe.magenta = new COLORREF(r, g, b);
                    break;
                case ConsoleColor.Yellow:
                    csbe.yellow = new COLORREF(r, g, b);
                    break;
                case ConsoleColor.White:
                    csbe.white = new COLORREF(r, g, b);
                    break;
            }

            SetBufferInfo(hConsoleOutput, csbe);
        }

        private static void SetBufferInfo(IntPtr hConsoleOutput, CONSOLE_SCREEN_BUFFER_INFO_EX csbe)
        {
            csbe.srWindow.Bottom++;
            csbe.srWindow.Right++;

            bool brc = SetConsoleScreenBufferInfoEx(hConsoleOutput, ref csbe);
            if (!brc)
            {
                throw CreateException(Marshal.GetLastWin32Error());
            }
        }

        private static Exception CreateException(int errorCode)
        {
            const int ERROR_INVALID_HANDLE = 6;
            if (errorCode == ERROR_INVALID_HANDLE) // Raised if the console is being run via another application, for example.
            {
                return new Exception("invalid handle");
            }

            return new Exception("error code: "+errorCode);
        }
    }
}
