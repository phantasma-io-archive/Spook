using Phantasma.Blockchain;
using Phantasma.Core.Log;
using System;

namespace Phantasma.CLI
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
                log.Message($"{tps.ToString("0.##")} TPS");
                txCount = 0;
            }
        }
    }
}
