using System;
using System.Threading;
using Phantasma.Blockchain;
using Phantasma.Core.Log;
using Phantasma.Spook.GUI;

namespace Phantasma.Spook.Plugins
{
    public class TPSPlugin : IChainPlugin, IPlugin
    {
        public string Channel => "tps";

        private int periodInSeconds;
        private DateTime lastTime = DateTime.UtcNow;
        private int txCount;
        private Logger logger;
        private Graph graph;
        private ConsoleGUI gui;

        public TPSPlugin(Logger logger, int periodInSeconds)
        {
            this.logger = logger;
            this.periodInSeconds = periodInSeconds;
            this.gui = logger as ConsoleGUI;

            if (gui != null)
            {
                this.graph = new Graph();
                gui.SetChannelGraph(Channel, graph);
            }
        }

        public override void OnTransaction(Chain chain, Block block, Transaction transaction)
        {
            Interlocked.Increment(ref txCount);
        }

        public void Update()
        {
            var currentTime = DateTime.UtcNow;
            var diff = (currentTime - lastTime).TotalSeconds;

            if (diff >= periodInSeconds)
            {
                lastTime = currentTime;
                var currentCount = Interlocked.Exchange(ref txCount, 0);
                var tps = currentCount / (float)periodInSeconds;

                var str = $"{tps.ToString("0.##")} TPS";
                if (gui != null)
                {
                    graph.Add(tps);
                    gui.WriteToChannel(Channel, LogEntryKind.Message, str);
                }
                else
                {
                    logger.Message(str);
                }
            }
        }
    }
}
