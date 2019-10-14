using Phantasma.Blockchain;
using Phantasma.Core.Log;
using Phantasma.Spook.GUI;
using System;

namespace Phantasma.Spook.Plugins
{
    public class MempoolPlugin : IPlugin
    {
        public string Channel => "mempool";

        public readonly Mempool Mempool;

        private int periodInSeconds;
        private DateTime lastTime = DateTime.MinValue;
        private Logger logger;
        private Graph graph;
        private ConsoleGUI gui;

        private long maxTxInPeriod = 0;

        public MempoolPlugin(Mempool mempool, Logger logger, int periodInSeconds)
        {
            this.Mempool = mempool;
            this.logger = logger;
            this.periodInSeconds = periodInSeconds;
            this.gui = logger as ConsoleGUI;

            if (gui != null)
            {
                this.graph = new Graph();
                gui.SetChannelGraph(Channel, graph);
            }
        }

        public void Update()
        {
            var txs = 0; // TODO FIX Mempool.Size;

            if (txs > maxTxInPeriod)
            {
                maxTxInPeriod = txs;
            }

            var currentTime = DateTime.UtcNow;
            var diff = (currentTime - lastTime).TotalSeconds;

            if (diff >= periodInSeconds)
            {
                lastTime = currentTime;

                var str = $"{maxTxInPeriod} txs";
                var gui = logger as ConsoleGUI;
                if (gui != null)
                {
                    graph.Add(maxTxInPeriod);
                    gui.WriteToChannel(Channel, LogEntryKind.Message, str);
                }
                else
                {
                    logger.Message(str);
                }
                maxTxInPeriod = 0;
            }
        }
    }
}
