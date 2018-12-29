using System;
using System.Collections.Generic;

namespace Phantasma.Spook.GUI
{
    public class GraphRenderer
    {
        private List<int> graphData = new List<int>();
        private int maxPoint = 0;

        public void ResetGraph()
        {
            maxPoint = 0;
            graphData.Clear();
        }

        public void AddGraphEntry(int val)
        {
            graphData.Add(val);

            if (val > maxPoint)
            {
                maxPoint = val;
            }
        }

        public void DisplayGraph(int curY, int maxLines)
        {
            int padLeft = maxPoint.ToString().Length + 1;

            int graphWidth = Console.WindowWidth - padLeft;

            int divisions = maxPoint / (maxLines + 1);
            if (divisions < 1)
            {
                divisions = 1;
            }

            for (int j = 0; j < maxLines; j++)
            {
                int n = (maxLines - j) * divisions;
                Console.SetCursorPosition(0, curY + j);
                Console.Write(n.ToString().PadRight(padLeft - 1));
                Console.Write('|');
            }

            int minPos = graphData.Count - graphWidth;
            if (minPos < 0)
            {
                minPos = 0;
            }

            int offset = graphWidth > graphData.Count ? graphWidth - graphData.Count : 0;

            for (int i = 0; i < graphWidth; i++)
            {
                int index = i + minPos - offset;
                int val = index >= 0 && index < graphData.Count ? graphData[index] : 0;
                val /= (divisions - 1);

                if (val > maxLines)
                {
                    val = maxLines;
                }

                if (val < maxLines / 3)
                {
                    Console.ForegroundColor = ConsoleColor.DarkBlue;
                }
                else
                if (val < maxLines / 2)
                {
                    Console.ForegroundColor = ConsoleColor.Blue;
                }
                else
                if (val < (maxLines / 3) * 2)
                {
                    Console.ForegroundColor = ConsoleColor.DarkCyan;
                }
                else
                {
                    Console.ForegroundColor = ConsoleColor.Cyan;
                }

                for (int j = 0; j < maxLines; j++)
                {
                    char ch;
                    if (j < val)
                    {
                        if (i > graphWidth / 2)
                        {
                            ch = '█';
                        }
                        else
                        if (i > graphWidth / 3)
                        {
                            ch = '▓';
                        }
                        else
                        if (i > graphWidth / 4)
                        {
                            ch = '▒';
                        }
                        else
                        {
                            ch = '░';
                        }
                    }
                    else
                    {
                        ch = ' ';
                    }

                    Console.SetCursorPosition(i + padLeft, curY + (maxLines - (1 + j)));
                    Console.Write(ch);
                }
            }
        }
    }
}
