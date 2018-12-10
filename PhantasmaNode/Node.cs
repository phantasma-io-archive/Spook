using System.Collections.Concurrent;
using System.Collections.Generic;
using Phantasma.Cryptography;
using Phantasma.Network.P2P;
using Phantasma.Core;
using Phantasma.Core.Log;
using System.Net.Sockets;
using System.Linq;
using System.Net;
using System;
using System.IO;
using Phantasma.Network.P2P.Messages;

namespace Phantasma.Blockchain.Consensus
{
    public sealed partial class Node: Runnable
    {
        public readonly int Port;
        public Address Address => keys.Address;

        public readonly KeyPair keys;
        public readonly Logger Log;

        public IEnumerable<Peer> Peers => _peers;

        private Mempool _mempool;

        private List<Peer> _peers = new List<Peer>();

        private TcpClient client;
        private TcpListener listener;

        private List<Endpoint> _activeSeeds = new List<Endpoint>();
        private List<Endpoint> _disabledSeeds = new List<Endpoint>();

        private bool listening = false;

        public Nexus Nexus { get; private set; }

        public Node(Nexus nexus, KeyPair keys, int port, IEnumerable<string> seeds, Logger log)
        {
            this.Nexus = nexus;
            this.Port = port;
            this.keys = keys;

            this.Log = Logger.Init(log);

            this._mempool = new Mempool(keys, nexus);

            this._activeSeeds = seeds.Select(x => ParseEndpoint(x)).ToList();

            // TODO this is a security issue, later change this to be configurable and default to localhost
            var bindAddress = IPAddress.Any;

            listener = new TcpListener(bindAddress, port);
            client = new TcpClient();
        }

        public Endpoint ParseEndpoint(string src)
        {
            int port;

            if (src.Contains(":"))
            {
                var temp = src.Split(':');
                Throw.If(temp.Length != 2, "Invalid endpoint format");
                src = temp[0];
                port = int.Parse(temp[1]);
            }
            else
            {
                port = this.Port;
            }

            return new Endpoint(src, port);
        }

        protected override bool Run()
        {
            lock (_peers)
            {
                if (ConnectToSeeds())
                {
                    return true;
                }
            }

            if (!listening)
            {
                Log.Debug("Waiting for new connections");
                listening = true;
                var accept = listener.BeginAcceptSocket(new AsyncCallback(DoAcceptSocketCallback), listener);
            }

            return true;
        }

        protected override void OnStart()
        {
            Log.Message($"Starting TCP listener on {Port}...");

            listener.Start();
        }

        protected override void OnStop()
        {
            listener.Stop();
        }

        private bool ConnectToSeeds()
        {
            if (_peers.Count == 0 && _activeSeeds.Count > 0)
            {
                var idx = Environment.TickCount % _activeSeeds.Count;
                var target = _activeSeeds[idx];

                var result = client.BeginConnect(target.Host, target.Port, null, null);

                var success = result.AsyncWaitHandle.WaitOne(TimeSpan.FromSeconds(1));

                if (!success)
                {
                    Log.Message("Could not reach seed " + target);
                    _disabledSeeds.Add(target);
                    _activeSeeds.RemoveAt(idx);
                }
                else
                {
                    Log.Message("Connected to seed " + target);
                    client.EndConnect(result);

                    HandleConnection(client.Client, true);
                    return true;
                }
            }

            return false;
        }

        // Process the client connection.
        public void DoAcceptSocketCallback(IAsyncResult ar)
        {
            listening = false;

            // Get the listener that handles the client request.
            TcpListener listener = (TcpListener)ar.AsyncState;

            Socket socket = listener.EndAcceptSocket(ar);
            Log.Message("New connection accepted from " + socket.RemoteEndPoint.ToString());

            HandleConnection(socket, false);
        }

        private void HandleConnection(Socket socket, bool sendIdentity)
        {
            var peer = new TCPPeer(Nexus, socket);
            _peers.Add(peer);

            if (sendIdentity)
            {
                var msg = new PeerIdentityMessage(this.Nexus, this.Address);
                peer.Send(msg);
                msg.Sign(this.keys);
            }

            while (true)
            {
                var msg = peer.Receive();
                if (msg == null)
                {
                    break;
                }

                Console.WriteLine("Got: " + msg.GetType().Name);

                var answer = HandleMessage(peer, msg);
                if (answer != null)
                {
                    answer.Sign(this.keys);
                    peer.Send(answer);
                }

            }

            socket.Close();

            // TODO remove peer from list
        }

        private Message HandleMessage(Peer peer, Message msg)
        {
            switch (msg.Opcode) {

                case Opcode.PEER_Identity:
                    {
                        // TODO add peer to list and send him list of peers
                        if (msg.IsSigned && msg.Address != Address.Null)
                        {
                            peer.SetAddress(msg.Address);
                            return null;
                        }
                        else {
                            return new ErrorMessage(Nexus, Address, P2PError.MessageShouldBeSigned);
                        }
                    }

                case Opcode.PEER_List:
                    {
                        // TODO check for any unknown peer and add to the list
                        break;
                    }

                case Opcode.MEMPOOL_Add:
                    {
                        break;
                    }

                case Opcode.MEMPOOL_List:
                    {
                        var txs = _mempool.GetTransactions();
                        break;
                    }

                case Opcode.BLOCKS_List:
                    {
                        break;
                    }

                case Opcode.CHAIN_List:
                    {
                        break;
                    }

                case Opcode.ERROR:
                    {
                        break;
                    }
            }

            return null;
        }


    }
}
