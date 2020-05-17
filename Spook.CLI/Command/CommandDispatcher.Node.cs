using System;


namespace Phantasma.Spook.Command
{
    partial class CommandDispatcher
    {
        [ConsoleCommand("node start", Category = "Node")]
        protected void OnStartCommand()
        {
            Console.WriteLine("Node starting...");
            _cli.Mempool.Start();
            _cli.Node.Start();
            Console.WriteLine("Node started");
        }

        [ConsoleCommand("node stop", Category = "Node")]
        protected void OnStopCommand()
        {
            Console.WriteLine("Node stopping...");
            _cli.Stop();
            Console.WriteLine("Node stopped");
        }

        [ConsoleCommand("node bounce", Category = "Node", Description = "Bounce a node to reload configuration")]
        protected void OnBounceCommand()
        {
            _cli.Stop();
            _cli.Start();
            Console.WriteLine("Node bounced");
        }
    }
}
