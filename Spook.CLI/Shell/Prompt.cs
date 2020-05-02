using System;
using System.IO;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using System.Xml.Serialization;
using System.Drawing;
using System.Threading;

namespace Phantasma.Spook.Shell
{
    public static class Prompt
    {
        private static string _prompt;
        private static int startingCursorLeft;
        private static int startingCursorTop;
        private static ConsoleKeyInfo key, lastKey;
        private static XmlSerializer xmls = new XmlSerializer(typeof(List<List<char>>)); // TODO use json
        private static List<string> historyList = new List<string>(); 
        private static List<List<char>> inputHistory = new List<List<char>>();
        public static bool running = true; 

        private static bool InputIsOnNewLine(List<char> input, int inputPosition)
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

        public static List<List<char>> GetHistory() 
        {
            return inputHistory;
        }

        private static void PersistHistory(string path = null) 
        {
            using (FileStream fs = new FileStream(path == null ? ".history" : path, FileMode.OpenOrCreate))
            {
                xmls.Serialize(fs, inputHistory);
            }
        }

        private static void LoadHistory(string path = null) 
        {
            try 
            {
                using (FileStream fs = new FileStream(path == null ? ".history" : path, FileMode.Open))
                {
                    try 
                    {
                        inputHistory = xmls.Deserialize(fs) as List<List<char>>;
                        fs.SetLength(0);
                    } 
                    catch (InvalidOperationException) 
                    {

                    }
                }
            }
            catch (FileNotFoundException)
            {

            }
        }

        private static void drawTextProgressBar(int progress, int total)
        {
         //draw empty progress bar
         Console.CursorLeft = 0;
         Console.Write("["); //start
         Console.CursorLeft = 32;
         Console.Write("]"); //end
         Console.CursorLeft = 1;
         float onechunk = 30.0f / total;
        
         //draw filled part
         int position = 1;
         for (int i = 0; i < onechunk * progress; i++)
         {
          Console.BackgroundColor = ConsoleColor.Gray;
          Console.CursorLeft = position++;
          Console.Write(" ");
         }
        
         //draw unfilled part
         for (int i = position; i <= 31; i++)
         {
          Console.BackgroundColor = ConsoleColor.Black;
          Console.CursorLeft = position++;
          Console.Write(" ");
         }
        
         //draw totals
         Console.CursorLeft = 35;
         Console.BackgroundColor = ConsoleColor.Black;
         Console.Write(progress.ToString() + " of " + total.ToString()+"    "); //blanks at the end remove any excess
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

        private static int mod(int x, int m)
        {
            return (x % m + m) % m;
        }

        private static void ClearLine(List<char> input, int inputPosition)
        {
            int cursorLeft = InputIsOnNewLine(input, inputPosition) ? 0 : _prompt.Length;
            Console.SetCursorPosition(cursorLeft, Console.CursorTop);
            Console.Write(new string(' ', input.Count + 5));
        }

        private static void ScrollBuffer(int lines = 0)
        {
            for (int i = 0; i <= lines; i++)
                Console.WriteLine("");
            Console.SetCursorPosition(0, Console.CursorTop - lines);
            startingCursorTop = Console.CursorTop - lines;
        }

        private static void RewriteLine(List<char> input, int inputPosition)
        {
            int cursorTop = 0;

            try
            {
                Console.SetCursorPosition(startingCursorLeft, startingCursorTop);
                var coords = GetCursorRelativePosition(input, inputPosition);
                cursorTop = startingCursorTop;
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
                Console.SetCursorPosition(mod(cursorLeft, Console.BufferWidth), cursorTop);
            }
            catch (Exception e)
            {
                Console.WriteLine(e.Message);
            }
        }

        private static IEnumerable<string> GetMatch(List<string> s, string input)
        {
            s.Add(input);
            int direction = (key.Modifiers == ConsoleModifiers.Shift) ? -1 : 1;
            for (int i = -1; i < s.Count; )
            {
                direction = (key.Modifiers == ConsoleModifiers.Shift) ? -1 : 1;
                i = mod((i + direction), s.Count);

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

        public static void ForEach<T>(this IEnumerable<T> source, Action<T> action)
        {   
            foreach (T item in source) { action(item); }
        }

        public static void Run(Func<string, List<char>, List<string>, string> lambda, string prompt,
                Func<string> PromptGenerator, string startupMsg, string history, List<string> completionList = null)
        {
            _prompt = PromptGenerator();
            Console.WriteLine(startupMsg);
            IEnumerator<string> wordIterator = null;
            LoadHistory(history);

            while (running)
            {
                string completion = null;
                List<char> input = new List<char>();
                startingCursorLeft = _prompt.Length;
                startingCursorTop = Console.CursorTop;
                int inputPosition = 0;
                int inputHistoryPosition = inputHistory.Count;

                key = lastKey = new ConsoleKeyInfo();
                Console.Write(_prompt);
                do
                {
                    _prompt = PromptGenerator();
                    key = Console.ReadKey(true);
                    if (key.Key == ConsoleKey.LeftArrow)
                    {
                        if (inputPosition > 0)
                        {
                            inputPosition--;
                            var pos = HandleMoveLeft(input, inputPosition);
                            Console.SetCursorPosition(pos.Item1, pos.Item2);
                        }
                    }
                    else if (key.Key == ConsoleKey.RightArrow)
                    {
                        if (inputPosition < input.Count)
                        {
                            var pos = HandleMoveRight(input, inputPosition++);
                            Console.SetCursorPosition(pos.Item1, pos.Item2);
                        }
                    }

                    else if (key.Key == ConsoleKey.Tab && completionList != null && completionList.Count > 0)
                    {
                        int tempPosition = inputPosition;
                        List<char> word = new List<char>();
                        while (tempPosition-- > 0 && !string.IsNullOrWhiteSpace(input[tempPosition].ToString()))
                            word.Insert(0, input[tempPosition]);

                        if (lastKey.Key == ConsoleKey.Tab)
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

                            List<string> hist = (from item in inputHistory select new String(item.ToArray())).ToList();

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
                    else if (key.Key == ConsoleKey.Home || (key.Key == ConsoleKey.H && key.Modifiers == ConsoleModifiers.Control))
                    {
                        inputPosition = 0;
                        Console.SetCursorPosition(prompt.Length, startingCursorTop);
                    }

                    else if (key.Key == ConsoleKey.End || (key.Key == ConsoleKey.E && key.Modifiers == ConsoleModifiers.Control))
                    {
                        inputPosition = input.Count;
                        var cursorLeft = 0;
                        int cursorTop = startingCursorTop;
                        if ((inputPosition + _prompt.Length) / Console.BufferWidth > 0)
                        {
                            cursorTop += (inputPosition + _prompt.Length) / Console.BufferWidth;
                            cursorLeft = (inputPosition + _prompt.Length) % Console.BufferWidth;
                        }
                        Console.SetCursorPosition(cursorLeft, cursorTop);
                    }

                    else if (key.Key == ConsoleKey.Delete)
                    {
                        if (inputPosition < input.Count)
                        {
                            input.RemoveAt(inputPosition);
                            ClearLine(input, inputPosition);
                            RewriteLine(input, inputPosition);
                        }
                    }

                    else if (key.Key == ConsoleKey.UpArrow)
                    {
                        if (inputHistoryPosition > 0)
                        {
                            inputHistoryPosition -= 1;
                            ClearLine(input, inputPosition);

                            // ToList() so we make a copy and don't use the reference in the list
                            input = inputHistory[inputHistoryPosition].ToList();
                            RewriteLine(input, input.Count);
                            inputPosition = input.Count;
                        }
                    }
                    else if (key.Key == ConsoleKey.DownArrow)
                    {
                        if (inputHistoryPosition < inputHistory.Count - 1)
                        {
                            inputHistoryPosition += 1;
                            ClearLine(input, inputPosition);

                            // ToList() so we make a copy and don't use the reference in the list
                            input = inputHistory[inputHistoryPosition].ToList();
                            RewriteLine(input, input.Count);
                            inputPosition = input.Count;
                        }
                        else
                        {
                            inputHistoryPosition = inputHistory.Count;
                            ClearLine(input, inputPosition);
                            Console.SetCursorPosition(prompt.Length, Console.CursorTop);
                            input = new List<char>();
                            inputPosition = 0;
                        }
                    }
                    else if (key.Key == ConsoleKey.Backspace)
                    {
                        if (inputPosition > 0)
                        {
                            inputPosition--;
                            input.RemoveAt(inputPosition);
                            ClearLine(input, inputPosition);
                            RewriteLine(input, inputPosition);
                        }
                    }

                    else if (key.Key == ConsoleKey.Escape)
                    {
                        if (lastKey.Key == ConsoleKey.Escape)
                            Environment.Exit(0);
                        else
                            Console.WriteLine("Press Escape again to exit.");
                    }

                    else if (key.Key == ConsoleKey.Enter && (key.Modifiers == ConsoleModifiers.Shift || key.Modifiers == ConsoleModifiers.Alt))
                    {
                        input.Insert(inputPosition++, (input.Count > 0) ? '\n' : ' ');
                        RewriteLine(input, inputPosition);
                    }

                    // multiline paste event
                    else if (key.Key == ConsoleKey.Enter && Console.KeyAvailable == true)
                    {
                        input.Insert(inputPosition++, '\n');
                        RewriteLine(input, inputPosition);
                    }

                    else if (key.Key != ConsoleKey.Enter)
                    {

                        input.Insert(inputPosition++, key.KeyChar);
                        RewriteLine(input, inputPosition);
                    }
                    else if (key.Key == ConsoleKey.Enter && input.Count == 0)
                    {

                        input.Insert(inputPosition++, ' ');
                        RewriteLine(input, inputPosition);
                    }
                    
                    lastKey = key;
                } while (!(key.Key == ConsoleKey.Enter && Console.KeyAvailable == false) 
                    // If Console.KeyAvailable = true then we have a multiline paste event
                    || (key.Key == ConsoleKey.Enter && (key.Modifiers == ConsoleModifiers.Shift 
                    || key.Modifiers == ConsoleModifiers.Alt)));

                Console.WriteLine("");
                var cmd = string.Concat(input);
                if (String.IsNullOrWhiteSpace(cmd))
                    continue;

                inputHistory.Add(input);
                PersistHistory();

                lambda(cmd, input, completionList);

            }
        }
    }
}
