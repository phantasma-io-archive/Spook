using System;


namespace Phantasma.Spook.Command
{
    partial class CommandDispatcher
    {
        [ConsoleCommand("simu timeskip", Category = "Simulator", Description="Skip minutes when using a simulator")]
        protected void OnSimuTimeSkipCommand(string[] args)
        {

            if (args.Length != 1)
            {
            throw new CommandException("Expected: minutes");
            }
            var minutes = int.Parse(args[0]);
            _cli.Simulator.CurrentTime += TimeSpan.FromMinutes(minutes);
            Console.WriteLine($"Simulator time advanced by {minutes}");
        }
    }
}
