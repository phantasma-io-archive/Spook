using System;
using System.IO;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using System.Xml.Serialization;

namespace Phantasma.Spook.Shell
{
    public static class Prompt
    {
        private static string _prompt;
        private static int _startingCursorLeft;
        private static int _startingCursorTop;
        private static ConsoleKeyInfo _key, _lastKey;
        private static readonly XmlSerializer xmls = new(typeof(List<List<char>>)); // TODO use json
        private static List<List<char>> _inputHistory = new();
        public static bool running = true; 

        private static bool InputIsOnNewLine(int inputPosition)
        {
            return (inputPosition + _prompt.Length > Console.BufferWidth - 1);
        }

        private static int GetCurrentLineForInput(List<char> input, int inputPosition)
        {
            int currentLine = 0;
            for (int i = 0; i < input.Count; i++)
            {
                if (input[i] == '\n')
                    currentLine += 1;
                if (i == inputPosition)
                    break;
            }
            return currentLine;
        }

        private static void PersistHistory(string path = null)
        {
            using var fs = new FileStream(path ?? ".history", FileMode.OpenOrCreate);
            xmls.Serialize(fs, _inputHistory);
        }

        private static void LoadHistory(string path = null) 
        {
            try
            {
                using var fs = new FileStream(path ?? ".history", FileMode.Open);
                try
                {
                    _inputHistory = xmls.Deserialize(fs) as List<List<char>>;
                    fs.SetLength(0);
                }
                catch (InvalidOperationException)
                {

                }
            }
            catch (FileNotFoundException)
            {

            }
        }

        private static Tuple<int,int> GetCursorRelativePosition(List<char> input, int inputPosition)
        {
            int currentPos = 0;
            int currentLine = 0;
            for (int i = 0; i < input.Count; i++)
            {
                if (input[i] == '\n')
                {
                    currentLine += 1;
                    currentPos = 0;
                }
                if (i == inputPosition)
                {
                    if (currentLine == 0)
                    {
                        currentPos += _prompt.Length;
                    }
                    break;
                }
                currentPos++;
            }
            return Tuple.Create(currentPos, currentLine);
        }

        private static int Mod(int x, int m)
        {
            return (x % m + m) % m;
        }

        private static void ClearLine(List<char> input, int inputPosition)
        {
            int cursorLeft = InputIsOnNewLine(inputPosition) ? 0 : _prompt.Length;
            Console.SetCursorPosition(cursorLeft, Console.CursorTop);
            Console.Write(new string(' ', input.Count + 5));
        }

        private static void ScrollBuffer(int lines = 0)
        {
            for (int i = 0; i <= lines; i++)
                Console.WriteLine("");
            Console.SetCursorPosition(0, Console.CursorTop - lines);
            _startingCursorTop = Console.CursorTop - lines;
        }

        private static void RewriteLine(List<char> input, int inputPosition)
        {
            int cursorTop = 0;

            try
            {
                Console.SetCursorPosition(_startingCursorLeft, _startingCursorTop);
                var coords = GetCursorRelativePosition(input, inputPosition);
                cursorTop = _startingCursorTop;
                int cursorLeft = 0;

                if (GetCurrentLineForInput(input, inputPosition) == 0)
                {
                    cursorTop += (inputPosition + _prompt.Length) / Console.BufferWidth;
                    cursorLeft = inputPosition + _prompt.Length;
                }
                else
                {
                    cursorTop += coords.Item2;
                    cursorLeft = coords.Item1 - 1;
                }

                // if the new vertical cursor position is going to exceed the buffer height (i.e., we are
                // at the bottom of console) then we need to scroll the buffer however much we are about to exceed by
                if (cursorTop >= Console.BufferHeight)
                {
                    ScrollBuffer(cursorTop - Console.BufferHeight + 1);
                    RewriteLine(input, inputPosition);
                    return;
                }

                Console.Write(String.Concat(input));
                Console.SetCursorPosition(Mod(cursorLeft, Console.BufferWidth), cursorTop);
            }
            catch (Exception e)
            {
                Console.WriteLine(e.Message);
            }
        }

        private static IEnumerable<string> GetMatch(List<string> s, string input)
        {
            s.Add(input);
            int direction = (_key.Modifiers == ConsoleModifiers.Shift) ? -1 : 1;
            for (int i = -1; i < s.Count; )
            {
                direction = (_key.Modifiers == ConsoleModifiers.Shift) ? -1 : 1;
                i = Mod((i + direction), s.Count);

                if (Regex.IsMatch(s[i], ".*(?:" + input + ").*", RegexOptions.IgnoreCase))
                {
                    yield return s[i];
                }
            }
        }

        static Tuple<int, int> HandleMoveLeft(List<char> input, int inputPosition)
        {
            var coords = GetCursorRelativePosition(input, inputPosition);
            int cursorLeftPosition = coords.Item1;
            int cursorTopPosition = Console.CursorTop;

            if (GetCurrentLineForInput(input, inputPosition) == 0)
                cursorLeftPosition = (coords.Item1) % Console.BufferWidth ;

            if (Console.CursorLeft == 0)
                cursorTopPosition = Console.CursorTop - 1;
            
            return Tuple.Create(cursorLeftPosition, cursorTopPosition);
        }

        static Tuple<int, int> HandleMoveRight(List<char> input, int inputPosition)
        {
            var coords = GetCursorRelativePosition(input, inputPosition);
            int cursorLeftPosition = coords.Item1;
            int cursorTopPosition = Console.CursorTop;
            if (Console.CursorLeft + 1 >= Console.BufferWidth || input[inputPosition] == '\n')
            {
                cursorLeftPosition = 0;
                cursorTopPosition = Console.CursorTop + 1;
            }
            return Tuple.Create(cursorLeftPosition % Console.BufferWidth, cursorTopPosition);
        }

        public static void Run(Func<string, List<char>, List<string>, string> lambda, string prompt,
                Func<string> promptGenerator, string startupMsg, string history, List<string> completionList = null)
        {
            _prompt = promptGenerator();
            Console.WriteLine(startupMsg);
            IEnumerator<string> wordIterator = null;
            LoadHistory(history);

            while (running)
            {
                string completion = null;
                List<char> input = new List<char>();
                _startingCursorLeft = _prompt.Length;
                _startingCursorTop = Console.CursorTop;
                int inputPosition = 0;
                int inputHistoryPosition = _inputHistory.Count;

                _key = _lastKey = new ConsoleKeyInfo();
                Console.Write(_prompt);
                do
                {
                    _prompt = promptGenerator();
                    _key = Console.ReadKey(true);
                    if (_key.Key == ConsoleKey.LeftArrow)
                    {
                        if (inputPosition > 0)
                        {
                            inputPosition--;
                            var pos = HandleMoveLeft(input, inputPosition);
                            Console.SetCursorPosition(pos.Item1, pos.Item2);
                        }
                    }
                    else if (_key.Key == ConsoleKey.RightArrow)
                    {
                        if (inputPosition < input.Count)
                        {
                            var pos = HandleMoveRight(input, inputPosition++);
                            Console.SetCursorPosition(pos.Item1, pos.Item2);
                        }
                    }

                    else if (_key.Key == ConsoleKey.Tab && completionList != null && completionList.Count > 0)
                    {
                        int tempPosition = inputPosition;
                        List<char> word = new List<char>();
                        while (tempPosition-- > 0 && !string.IsNullOrWhiteSpace(input[tempPosition].ToString()))
                            word.Insert(0, input[tempPosition]);

                        if (_lastKey.Key == ConsoleKey.Tab)
                        {
                            wordIterator.MoveNext();
                            if (completion != null)
                            {
                                ClearLine(input, inputPosition);
                                for (var i = 0; i < completion.Length; i++)
                                {
                                    input.RemoveAt(--inputPosition);
                                }
                                RewriteLine(input, inputPosition);
                            }
                            else
                            {
                                ClearLine(input, inputPosition);
                                for (var i = 0; i < string.Concat(word).Length; i++)
                                {
                                    input.RemoveAt(--inputPosition);
                                }
                                RewriteLine(input, inputPosition);
                            }
                        }
                        else
                        {
                            ClearLine(input, inputPosition);
                            for (var i = 0; i < string.Concat(word).Length; i++)
                            {
                                input.RemoveAt(--inputPosition);
                            }
                            RewriteLine(input, inputPosition);

                            List<string> hist = (from item in _inputHistory select new String(item.ToArray())).ToList();

                            List<string> wordList = completionList.Concat(hist).ToList();

                            wordIterator = GetMatch(wordList, string.Concat(word)).GetEnumerator();

                            while (wordIterator.Current == null)
                                wordIterator.MoveNext();
                        }

                        completion = wordIterator.Current;
                        ClearLine(input, inputPosition);
                        foreach (var c in completion.ToCharArray())
                        {
                            input.Insert(inputPosition++, c);
                        }
                        RewriteLine(input, inputPosition);

                    }
                    else if (_key.Key == ConsoleKey.Home || (_key.Key == ConsoleKey.H && _key.Modifiers == ConsoleModifiers.Control))
                    {
                        inputPosition = 0;
                        Console.SetCursorPosition(prompt.Length, _startingCursorTop);
                    }

                    else if (_key.Key == ConsoleKey.End || (_key.Key == ConsoleKey.E && _key.Modifiers == ConsoleModifiers.Control))
                    {
                        inputPosition = input.Count;
                        var cursorLeft = 0;
                        int cursorTop = _startingCursorTop;
                        if ((inputPosition + _prompt.Length) / Console.BufferWidth > 0)
                        {
                            cursorTop += (inputPosition + _prompt.Length) / Console.BufferWidth;
                            cursorLeft = (inputPosition + _prompt.Length) % Console.BufferWidth;
                        }
                        Console.SetCursorPosition(cursorLeft, cursorTop);
                    }

                    else if (_key.Key == ConsoleKey.Delete)
                    {
                        if (inputPosition < input.Count)
                        {
                            input.RemoveAt(inputPosition);
                            ClearLine(input, inputPosition);
                            RewriteLine(input, inputPosition);
                        }
                    }

                    else if (_key.Key == ConsoleKey.UpArrow)
                    {
                        if (inputHistoryPosition > 0)
                        {
                            inputHistoryPosition -= 1;
                            ClearLine(input, inputPosition);

                            // ToList() so we make a copy and don't use the reference in the list
                            input = _inputHistory[inputHistoryPosition].ToList();
                            RewriteLine(input, input.Count);
                            inputPosition = input.Count;
                        }
                    }
                    else if (_key.Key == ConsoleKey.DownArrow)
                    {
                        if (inputHistoryPosition < _inputHistory.Count - 1)
                        {
                            inputHistoryPosition += 1;
                            ClearLine(input, inputPosition);

                            // ToList() so we make a copy and don't use the reference in the list
                            input = _inputHistory[inputHistoryPosition].ToList();
                            RewriteLine(input, input.Count);
                            inputPosition = input.Count;
                        }
                        else
                        {
                            inputHistoryPosition = _inputHistory.Count;
                            ClearLine(input, inputPosition);
                            Console.SetCursorPosition(prompt.Length, Console.CursorTop);
                            input = new List<char>();
                            inputPosition = 0;
                        }
                    }
                    else if (_key.Key == ConsoleKey.Backspace)
                    {
                        if (inputPosition > 0)
                        {
                            inputPosition--;
                            input.RemoveAt(inputPosition);
                            ClearLine(input, inputPosition);
                            RewriteLine(input, inputPosition);
                        }
                    }

                    else if (_key.Key == ConsoleKey.Escape)
                    {
                        if (_lastKey.Key == ConsoleKey.Escape)
                            Environment.Exit(0);
                        else
                            Console.WriteLine("Press Escape again to exit.");
                    }

                    else if (_key.Key == ConsoleKey.Enter && (_key.Modifiers == ConsoleModifiers.Shift || _key.Modifiers == ConsoleModifiers.Alt))
                    {
                        input.Insert(inputPosition++, (input.Count > 0) ? '\n' : ' ');
                        RewriteLine(input, inputPosition);
                    }

                    // multiline paste event
                    else if (_key.Key == ConsoleKey.Enter && Console.KeyAvailable == true)
                    {
                        input.Insert(inputPosition++, '\n');
                        RewriteLine(input, inputPosition);
                    }

                    else if (_key.Key != ConsoleKey.Enter)
                    {

                        input.Insert(inputPosition++, _key.KeyChar);
                        RewriteLine(input, inputPosition);
                    }
                    else if (_key.Key == ConsoleKey.Enter && input.Count == 0)
                    {

                        input.Insert(inputPosition++, ' ');
                        RewriteLine(input, inputPosition);
                    }
                    
                    _lastKey = _key;
                } while (!(_key.Key == ConsoleKey.Enter && Console.KeyAvailable == false)
                    // If Console.KeyAvailable = true then we have a multiline paste event
                    || (_key.Key == ConsoleKey.Enter && (_key.Modifiers == ConsoleModifiers.Shift
                    || _key.Modifiers == ConsoleModifiers.Alt)));

                Console.WriteLine("");
                var cmd = string.Concat(input);
                if (string.IsNullOrWhiteSpace(cmd))
                    continue;

                _inputHistory.Add(input);
                PersistHistory(history);

                lambda(cmd, input, completionList);

            }
        }
    }
}
