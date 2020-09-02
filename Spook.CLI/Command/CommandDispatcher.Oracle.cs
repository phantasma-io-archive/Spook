using System;
using Phantasma.Cryptography;


namespace Phantasma.Spook.Command
{
    partial class CommandDispatcher
    {
        [ConsoleCommand("oracle read", Category = "Oracle", Description="Read a transaction from an oracle")]
        protected void OnOracleReadCommand(string[] args)
        {
            // currently neo only, revisit for eth 
            var hash = Hash.Parse(args[0]);
            var reader = _cli.Nexus.GetOracleReader();
            var tx = reader.ReadTransaction("neo", "neo", hash);

            // not sure if that's exactly what we want, probably needs more output...
            Console.WriteLine(tx.Transfers[0].interopAddress.Text);
        }
        [ConsoleCommand("platform height get", Category = "Oracle", Description = "Get platform height")]
        protected void OnPlatformHeightGet(string[] args)
        {
            var reader = _cli.Nexus.GetOracleReader();

            Console.WriteLine($"Platform {args[0]} [chain {args[1]}] current height: {reader.GetCurrentHeight(args[0], args[1])}");
        }
        [ConsoleCommand("platform height set", Category = "Oracle", Description = "Set platform height")]
        protected void OnPlatformHeightSet(string[] args)
        {
            Console.WriteLine($"Setting platform {args[0]} [chain {args[1]}] height {args[2]} ()...");
            lock (String.Intern("PendingSetCurrentHeight_" + args[0]))
            {
                var reader = _cli.Nexus.GetOracleReader();
                reader.SetCurrentHeight(args[0], args[1], args[2]);

                Console.WriteLine($"Height {args[2]} is set for platform {args[0]}, chain {args[1]}");
                _cli.Logger.Message($"Height {args[2]} is set for platform {args[0]}, chain {args[1]}");
            }
        }
    }
}
