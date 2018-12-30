using System;
using System.Collections.Generic;

namespace Phantasma.Spook.GUI
{
    public class Graph
    {
        public float maxPoint { get; private set; }
        public List<float> data { get; private set; } = new List<float>();
        public Func<float, string> formatter;

        public Graph()
        {
            Reset();
        }

        public void Reset()
        {
            maxPoint = 0;
            data.Clear();
            formatter = (x) => x.ToString();
        }

        public void Add(float val)
        {
            if (data.Count > 1024)
            {
                data.RemoveAt(0);
            }

            data.Add(val);

            if (val > maxPoint)
            {
                maxPoint = val;
            }
        }
    }
}
