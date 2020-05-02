using System.Globalization;
using System.Threading;
using Phantasma.Spook;
using Phantasma.Spook.Shell;

namespace Neo
{
    static class Program
    {
        static void Main(string[] args)
        {
            var culture = new CultureInfo("en-US");
            Thread.CurrentThread.CurrentCulture = culture;
            CultureInfo.DefaultThreadCurrentCulture = culture;
            var _settings = new SpookSettings(args);

            var CLI = new CLI(args, _settings);

            if (_settings.App.UseShell)
            {
                new SpookShell(args, _settings, CLI);
                return;
            }
        }
    }
}
