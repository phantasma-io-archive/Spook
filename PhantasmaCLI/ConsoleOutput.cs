using Phantasma.Core.Log;
using System;
using System.Collections.Generic;

namespace Phantasma.CLI
{
    [Flags]
    public enum RedrawFlags
    {
        None = 0,
        Logo = 0x1,
        Prompt = 0x2,
        Log = 0x4
    }

    public class ConsoleOutput: Logger
    {
        private byte[] logo;
        private ConsoleColor defaultBG;
        private List<KeyValuePair<LogEntryKind, string>> _text = new List<KeyValuePair<LogEntryKind, string>>();
        private int lastIndex;
        private RedrawFlags redrawFlags = RedrawFlags.None;

        private bool ready = false;
        private bool initializing = true;
        private int animationCounter = 0;
        private DateTime lastRedraw;

        private string prompt = "";

        public ConsoleOutput()
        {
            Console.ResetColor();
            this.defaultBG = Console.BackgroundColor;
            this.logo = Logo.GetPixels();
            this.redrawFlags = RedrawFlags.Logo | RedrawFlags.Prompt;
           
            Update();
        }

        public void MakeReady()
        {
            ready = true;
        }

        public override void Write(LogEntryKind kind, string msg)
        {
            InternalWrite(kind, msg);
        }

        private void InternalWrite(LogEntryKind kind, string msg)
        {
            lock (_text)
            {
                _text.Add(new KeyValuePair<LogEntryKind, string>(kind, msg));
                redrawFlags |= RedrawFlags.Log;
            }
        }

        private void FillLine(ConsoleColor fg, char symbol)
        {
            Console.ForegroundColor = fg;

            int maxX = Console.WindowWidth;
            if (Console.CursorTop >= Console.WindowHeight-1)
            {
                maxX--;
            }

            for (int i= Console.CursorLeft; i <maxX; i++)
            {
                Console.Write(symbol);
            }
        }

        private void Redraw()
        {
            //Console.Clear();
            Console.CursorVisible = false;

            int lY = 1;
            if (redrawFlags.HasFlag(RedrawFlags.Logo))
            {
                redrawFlags &= ~RedrawFlags.Logo;

                Console.SetCursorPosition(0, 0);
                FillLine(ConsoleColor.DarkCyan, '.');

                int midX = Console.WindowWidth / 2;
                int lX = midX - (Logo.Width / 2);

                for (int j = 0; j < Logo.Height; j++)
                {
                    Console.SetCursorPosition(lX, j + lY);
                    for (int i = 0; i < Logo.Width; i++)
                    {
                        var pixel = logo[i + j * Logo.Width];
                        switch (pixel)
                        {
                            case 1: Console.BackgroundColor = ConsoleColor.DarkCyan; break;
                            case 2: Console.BackgroundColor = ConsoleColor.Cyan; break;
                            case 3: Console.BackgroundColor = ConsoleColor.White; break;
                            default: Console.BackgroundColor = defaultBG; break;
                        }
                        Console.Write(" ");
                    }
                }
            }

            Console.BackgroundColor = defaultBG;
            Console.ForegroundColor = ConsoleColor.DarkGray;

            int curY = Logo.Height + lY;

            if (redrawFlags.HasFlag(RedrawFlags.Prompt))
            {
                redrawFlags &= ~RedrawFlags.Prompt;

                Console.SetCursorPosition(0, curY);

                if (initializing)
                {
                    Console.Write("Booting Phantasma node");
                    int dots = animationCounter % 4;
                    for (int i = 0; i < dots; i++)
                    {
                        Console.Write(".");
                    }
                }
                else
                {
                    Console.Write(">"+prompt);
                    if (animationCounter % 2 == 0)
                    {
                        Console.Write("_");
                    }
                }
                FillLine(ConsoleColor.White, ' ');

                Console.SetCursorPosition(0, curY + 1);
                FillLine(ConsoleColor.DarkCyan, '.');
            }

            curY++;
            if (redrawFlags.HasFlag(RedrawFlags.Log))
            {
                redrawFlags &= ~RedrawFlags.Log;


                curY++;
                int maxLines = (Console.WindowHeight - 1) - (curY + 1);

                for (int i = 0; i < _text.Count; i++)
                {
                    Console.SetCursorPosition(0, curY + i);

                    var entry = _text[i];
                    switch (entry.Key)
                    {
                        case LogEntryKind.Error: Console.ForegroundColor = ConsoleColor.Red; break;
                        case LogEntryKind.Warning: Console.ForegroundColor = ConsoleColor.Yellow; break;
                        case LogEntryKind.Sucess: Console.ForegroundColor = ConsoleColor.Green; break;
                        case LogEntryKind.Debug: Console.ForegroundColor = ConsoleColor.Cyan; break;
                        default: Console.ForegroundColor = ConsoleColor.Gray; break;
                    }

                    Console.Write(entry.Value);
                    FillLine(ConsoleColor.DarkCyan, ' ');

                    if (i >= maxLines)
                    {
                        if (_text.Count > maxLines)
                        {
                            _text.RemoveAt(0);
                            redrawFlags |= RedrawFlags.Log;

                            if (_text.Count == maxLines && ready)
                            {
                                initializing = false;
                                ready = false;
                                redrawFlags |= RedrawFlags.Log | RedrawFlags.Logo | RedrawFlags.Prompt;
                            }
                        }
                        break;
                    }
                }

                Console.SetCursorPosition(0, Console.WindowHeight - 1);
                FillLine(ConsoleColor.DarkCyan, '.');
            }
        }

        public void Update()
        {
            if (!initializing && Console.KeyAvailable)
            {
                var press = Console.ReadKey();

                if (press.KeyChar>=32 && press.KeyChar<=127)
                {
                    prompt += press.KeyChar;
                    redrawFlags |= RedrawFlags.Prompt;
                }
                else
                {
                    switch (press.Key)
                    {
                        case ConsoleKey.Backspace:
                            {
                                if (!string.IsNullOrEmpty(prompt))
                                {
                                    prompt = prompt.Substring(0, prompt.Length - 1);
                                    redrawFlags |= RedrawFlags.Prompt;
                                }
                                break;
                            }
                    }
                }
            }

            var diff = DateTime.UtcNow - lastRedraw;
            if (diff.TotalSeconds >= 1)
            {
                lastRedraw = DateTime.UtcNow;
                animationCounter++;
                redrawFlags |= RedrawFlags.Prompt;
            }

            if (redrawFlags != RedrawFlags.None) {
                lock (_text)
                {
                    Redraw();
                }
            }
        }
    }
}
