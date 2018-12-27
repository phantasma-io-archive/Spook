using Phantasma.Core.Log;
using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;

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

    public class ConsoleGUI: Logger
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

        private CommandDispatcher dispatcher;

        private string prompt = "";

        public ConsoleGUI()
        {
            Console.ResetColor();
            this.defaultBG = Console.BackgroundColor;
            this.logo = Logo.GetPixels();
            this.redrawFlags = RedrawFlags.Logo | RedrawFlags.Prompt;

            if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
            {
                var colors = ColorMapper.GetBufferColors();
                colors[ConsoleColor.DarkCyan] = new COLORREF(52, 133, 157);
                colors[ConsoleColor.Cyan] = new COLORREF(126, 196, 193);
                colors[ConsoleColor.Yellow] = new COLORREF(245, 237, 186);
                colors[ConsoleColor.Red] = new COLORREF(210, 100, 103);
                colors[ConsoleColor.Green] = new COLORREF(192, 199, 65);
                ColorMapper.SetBatchBufferColors(colors);
            }

            Update();
        }

        public void MakeReady(CommandDispatcher dispatcher)
        {
            this.dispatcher = dispatcher;
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

        private void FillLine(char symbol)
        {
            Console.Write(new string(symbol, (Console.WindowWidth - 1) - Console.CursorLeft));
            return;
        }

        private void Redraw()
        {
            //Console.Clear();
            Console.CursorVisible = false;

            int lY = 0;
            if (redrawFlags.HasFlag(RedrawFlags.Logo))
            {
                redrawFlags &= ~RedrawFlags.Logo;

                int midX = Console.WindowWidth / 2;
                int lX = midX - (Logo.Width / 2);

                Console.ForegroundColor = ConsoleColor.DarkBlue;

                for (int j = 0; j < Logo.Height; j++)
                {
                    Console.SetCursorPosition(lX, j + lY);
                    for (int i = 0; i < Logo.Width; i++)
                    {
                        var pixel = logo[i + j * Logo.Width];
                        if (pixel == 0)
                        {
                            Console.CursorLeft++;
                            continue;
                        }

                        switch (pixel)
                        {
                            case 1: Console.BackgroundColor = ConsoleColor.DarkCyan; break;
                            case 2: Console.BackgroundColor = ConsoleColor.Cyan; break;
                            case 3: Console.BackgroundColor = ConsoleColor.Yellow; break;
                            default: Console.BackgroundColor = defaultBG; break;
                        }
                        Console.Write(" ");
                    }
                }
            }

            Console.BackgroundColor = defaultBG;
            Console.ForegroundColor = ConsoleColor.DarkGray;

            if (redrawFlags.HasFlag(RedrawFlags.Prompt))
            {
                redrawFlags &= ~RedrawFlags.Prompt;

                Console.SetCursorPosition(0, Console.WindowHeight - 2);

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
                    Console.Write(">");

                    if (!string.IsNullOrEmpty(prompt))
                    {
                        Console.Write(prompt);
                    }

                    if (animationCounter % 2 == 0)
                    {
                        Console.Write("_");
                    }
                }

                FillLine(' ');
            }

            if (redrawFlags.HasFlag(RedrawFlags.Log))
            {
                redrawFlags &= ~RedrawFlags.Log;

                int curY = Logo.Height + lY;
                Console.SetCursorPosition(0, curY);
                FillLine('.');

                curY++;
                int maxLines = (Console.WindowHeight - 1) - (curY + 1);

                for (int i = 0; i < _text.Count; i++)
                {
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

                    if (entry.Value.Length > Console.WindowWidth - 1)
                    {
                        var str = entry.Value.Substring(0, Console.WindowWidth-4)+"...";
                        Console.Write(str);
                    }
                    else
                    {
                        Console.Write(entry.Value);
                    }
                    FillLine(' ');
                }
            }
        }
             
        private void CheckKeys()
        {
            if (!Console.KeyAvailable)
            {
                return;
            }

            var press = Console.ReadKey();

            if (press.KeyChar >= 32 && press.KeyChar <= 127)
            {
                prompt += press.KeyChar;
                redrawFlags |= RedrawFlags.Prompt;
            }
            else
            {
                switch (press.Key)
                {
                    case ConsoleKey.Enter:
                        {
                            if (!string.IsNullOrEmpty(prompt))
                            {
                                if (dispatcher != null)
                                {
                                    try
                                    {
                                        dispatcher.ExecuteCommand(prompt);
                                    }
                                    catch (CommandException e)
                                    {
                                        Write(LogEntryKind.Warning, e.Message);
                                    }
                                    catch (Exception e)
                                    {
                                        Write(LogEntryKind.Error, e.ToString());
                                    }
                                }
                                prompt = "";
                            }
                            break;
                        }

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

        public void Update()
        {
            if (!initializing)
            {
                CheckKeys();
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
