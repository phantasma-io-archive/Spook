using System;
using Phantasma.Core.Log;
using Phantasma.Spook.Utils;

namespace Phantasma.Spook
{
    public class ShellLogger : FileLogger
    {
        public ShellLogger(string file) : base(file)
        {
            
        }

        public override void Write(LogEntryKind kind, string msg)
        {
            // only output stuff that should be shown in the shell
            if (kind == LogEntryKind.Shell)
            {
                try
                {
                    if (msg.IsValidJson())
                    {
                        msg = SpookUtils.FormatJson(msg);
                    }
                }
                catch (Exception) {}

                Console.WriteLine(msg);
            }

            base.Write(kind, msg);
        }
    }
}
