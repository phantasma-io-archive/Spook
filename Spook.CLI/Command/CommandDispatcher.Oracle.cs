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
    }
}
