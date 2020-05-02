using System;
using System.IO;
using Phantasma.VM.Utils;
using Phantasma.Cryptography;
using Phantasma.Blockchain;
using Phantasma.API;
using System.Linq;
using Phantasma.Core.Log;
using Phantasma.Numerics;
using Phantasma.Core.Types;
using System.Threading;
using Phantasma.Blockchain.Contracts;
using Phantasma.Domain;
using Phantasma.Spook.Command;

namespace Phantasma.Spook.Modules
{
    [Module("file")]
    public static class FileModule
    {
        public static Logger logger => ModuleLogger.Instance;

        public static void Upload(string txIdentifier, PhantasmaKeys source, NexusAPI api, string[] args)
        {
            if (args.Length != 1)
            {
                throw new CommandException("Expected args: file_path");
            }

            var filePath = args[0];

            if (!File.Exists(filePath))
            {
                throw new CommandException("File does not exist");
            }

            var fileContent = File.ReadAllBytes(filePath);
            var contentMerkle = new MerkleTree(fileContent);

            var fileName = Path.GetFileName(filePath);

            var script = ScriptUtils.BeginScript().
                AllowGas(source.Address, Address.Null, 1, 9999).
                CallContract("storage", "UploadFile", source.Address, fileName, fileContent.Length, contentMerkle, ArchiveFlags.None, new byte[0]).
                SpendGas(source.Address).
                EndScript();
            var tx = new Transaction(api.Nexus.Name, "main", script, Timestamp.Now + TimeSpan.FromMinutes(5), txIdentifier);
            tx.Sign(source);
            var rawTx = tx.ToByteArray(true);

            logger.Shell($"Uploading {fileName}...");
            try
            {
                api.SendRawTransaction(Base16.Encode(rawTx));
            }
            catch (Exception e)
            {
                throw new CommandException(e.Message);
            }

            Thread.Sleep(3000);
            var hash = tx.Hash.ToString();
            do
            {
                try
                {
                    var result = api.GetTransaction(hash);
                }
                catch (Exception e)
                {
                    throw new CommandException(e.Message);
                }
                /*if (result is ErrorResult)
                {
                    var temp = (ErrorResult)result;
                    if (temp.error.Contains("pending"))
                    {
                        Thread.Sleep(1000);
                    }
                    else
                    {
                        throw new CommandException(temp.error);
                    }
                }
                else*/
                {
                    break;
                }
            } while (true);

            var archiveHash = contentMerkle.Root.ToString();
            var archive = (ArchiveResult)api.GetArchive(archiveHash);
            for (int i = 0; i < archive.blockCount; i++)
            {
                var ofs = (int)(i * Archive.BlockSize);
                var blockContent = fileContent.Skip(ofs).Take((int)Archive.BlockSize).ToArray();

                logger.Message($"Writing block {i+1} out of {archive.blockCount}");
                api.WriteArchive(archiveHash, i, Base16.Encode(blockContent));
            }

            logger.Shell($"File uploaded successfully!");
        }
    }
}
