using Phantasma.Spook.Modules;
using Phantasma.Numerics;

namespace Phantasma.Spook.Command
{
    partial class CommandDispatcher
    {
        [ConsoleCommand("sale buyers", Category = "Sale", Description = "Get lists of addresses that participated on a sale")]
        protected void OnSaleBuyersCommand(string[] args)
        {
            SaleModule.GetBuyers(args, _cli.Nexus);
        }

        [ConsoleCommand("sale finish", Category = "Sale", Description = "Either distributes or refunds a sale")]
        protected void OnSaleFinishCommand(string[] args)
        {
            var minFee = new BigInteger(_cli.Settings.Node.MinimumFee);
            SaleModule.Finish(args, _cli.NexusAPI, minFee);
        }
    }
}
