using Phantasma.Blockchain;
using Phantasma.Core.Log;
using Phantasma.Spook.GUI;
using System;

namespace Phantasma.Spook.Plugins
{
    public class TPSPlugin : IChainPlugin
    {
        private int periodInSeconds;
        private int txCount;
        private DateTime lastTime = DateTime.UtcNow;
        private Logger log;

        public TPSPlugin(Logger log, int periodInSeconds)
        {
            this.log = log;
            this.periodInSeconds = periodInSeconds;
        }

        public override void OnTransaction(Chain chain, Block block, Transaction transaction)
        {
            txCount++;

            var currentTime = DateTime.UtcNow;
            var diff = (currentTime - lastTime).TotalSeconds;

            if (diff >= periodInSeconds)
            {
                lastTime = currentTime;
                var tps = txCount / (float)periodInSeconds;

                var str = $"{tps.ToString("0.##")} TPS";
                var gui = log as ConsoleGUI;
                if (gui != null)
                {
                    gui.AddGraphEntry("tps", (int)tps);
                    gui.WriteToChannel("tps", LogEntryKind.Message, str);
                }
                else
                {
                    log.Message(str);
                }
                txCount = 0;
            }
        }
    }
}
