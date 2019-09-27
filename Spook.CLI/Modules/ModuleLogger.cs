using Phantasma.Core.Log;
using Phantasma.Spook.GUI;

namespace Phantasma.Spook.Modules
{
    public class ModuleLogger : Logger
    {
        private ConsoleGUI gui;
        private Logger baseLogger;

        public static ModuleLogger Instance;

        public ModuleLogger(Logger baseLogger, ConsoleGUI gui)
        {
            this.baseLogger = baseLogger;
            this.gui = gui;
        }

        public override void Write(LogEntryKind kind, string msg)
        {
            baseLogger.Write(kind, msg);
            gui?.Update();
        }

        public static void Init(Logger logger, ConsoleGUI gui)
        {
            Instance = new ModuleLogger(logger, gui);
        }
    }
}
