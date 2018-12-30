using Phantasma.Core.Log;
using Phantasma.Spook.GUI;
using System;

namespace Phantasma.Spook.Plugins
{
    public class RAMPlugin: IPlugin
    {
        public string Channel => "ram";

        private int periodInSeconds;
        private DateTime lastTime = DateTime.MinValue;
        private Logger logger;
        private ConsoleGraph graph;
        private ConsoleGUI gui;

        private long maxMemoryInPeriod = 0;

        public RAMPlugin(Logger logger, int periodInSeconds)
        {
            this.logger = logger;
            this.periodInSeconds = periodInSeconds;
            this.gui = logger as ConsoleGUI;

            if (gui != null)
            {
                this.graph = new ConsoleGraph();
                graph.formatter = FormatMemoryAmount;
                gui.SetChannelGraph(Channel, graph);
            }
        }

        private string FormatMemoryAmount(float x)
        {
            if (x < 1000)
            {
                return x.ToString("0.##") + "kb";
            }

            x /= 1000;

            if (x < 1000)
            {
                return x.ToString("0.##") + "mb";
            }

            x /= 1000;
            return x.ToString("0.##") + "gb";
        }

        public void Update()
        {
            var ram = GC.GetTotalMemory(false);

            if (ram > maxMemoryInPeriod)
            {
                maxMemoryInPeriod = ram;
            }

            var currentTime = DateTime.UtcNow;
            var diff = (currentTime - lastTime).TotalSeconds;

            if (diff >= periodInSeconds)
            {
                lastTime = currentTime;

                var mb = (maxMemoryInPeriod / 1024.0f);

                var str = FormatMemoryAmount(mb);
                var gui = logger as ConsoleGUI;
                if (gui != null)
                {
                    graph.Add(mb);
                    gui.WriteToChannel(Channel, LogEntryKind.Message, str);
                }
                else
                {
                    logger.Message(str);
                }
                maxMemoryInPeriod = 0;
            }
        }
    }
}
