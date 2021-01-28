using System;


namespace Phantasma.Spook.Command
{
    partial class CommandDispatcher
    {
        [ConsoleCommand("mempool list", Category = "Mempool", Description="List all transactions currently in mempool")]
        protected void OnMempoolListCommand(string[] args)
        {
            foreach (var tx in _cli.Mempool.GetTransactions())
            {
                Console.WriteLine(tx.ToString());
            }
        }
    }
}
