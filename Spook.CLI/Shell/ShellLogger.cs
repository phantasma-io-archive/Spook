using System;
using Phantasma.Core.Log;
using Phantasma.Spook.Utils;

namespace Phantasma.Spook
{
    public class ShellLogger : FileLogger
    {
        private ConsoleLogger consoleLogger;

        public ShellLogger(string file) : base(file)
        {
            consoleLogger = new ConsoleLogger();
        }

        public override void Write(LogEntryKind kind, string msg)
        {
            // only output stuff that should be shown in the shell
            if (kind == LogEntryKind.Message)
            {
                try
                {
                    if (msg.IsValidJson())
                    {
                        msg = SpookUtils.FormatJson(msg);
                    }
                }
                catch (Exception) {}
            }

            consoleLogger.Write(kind, msg);
            base.Write(kind, msg);
        }
    }
}
