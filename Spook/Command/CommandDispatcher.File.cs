using Phantasma.Spook.Modules;


namespace Phantasma.Spook.Command
{
    partial class CommandDispatcher
    {
        [ConsoleCommand("file upload", Category = "Storage", Description="Uploads a file to phantasma storage")]
        protected void OnFileUploadCommand(string[] args)
        {
            FileModule.Upload(Spook.Identifier, WalletModule.Keys, _cli.NexusAPI, args);
        }
    }
}
