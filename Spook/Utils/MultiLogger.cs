using Phantasma.Core.Log;
using System;
using System.Collections.Generic;

namespace Phantasma.Spook.Utils
{
    // Allows routing same message to multiple loggers
    public class MultiLogger : Logger
    {
        private IEnumerable<Logger> _loggers;

        public MultiLogger(IEnumerable<Logger> loggers) : base(LogLevel.Maximum)
        {
            _loggers = loggers;
        }

        protected override void Write(LogLevel kind, string msg)
        {
            if (kind == LogLevel.Message)
            {
                try
                {
                    if (msg.IsValidJson())
                    {
                        msg = SpookUtils.FormatJson(msg);
                    }
                }
                catch (Exception) { }
            }

            foreach (var logger in _loggers)
            {
                logger.RouteMessage(kind, msg);
            }
        }
    }
}
