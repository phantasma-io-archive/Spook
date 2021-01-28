using System.Globalization;
using System.Threading;
using Phantasma.Spook.Shell;

namespace Phantasma.Spook
{
    static class Program
    {
        static void Main(string[] args)
        {
            var culture = new CultureInfo("en-US");
            Thread.CurrentThread.CurrentCulture = culture;
            CultureInfo.DefaultThreadCurrentCulture = culture;

            var node = new Spook(args);

            if (node.Settings.App.UseShell)
            {
                new SpookShell(args, node.Settings, node);
            }
            else
            {
                node.Start();
            }
        }
    }
}
