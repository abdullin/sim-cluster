using System;
using System.Collections.Generic;
using System.IO;
using System.Net.Sockets;
using System.Threading;
using System.Threading.Tasks;

namespace SimMach.Sim {

  
    
    sealed class SimNetwork {
        readonly SimRuntime _runtime;

        public SimNetwork(NetworkDef def, SimRuntime runtime) {
            _runtime = runtime;

            // we register each link as a network service
            foreach (var link in def.Links) {
                var service = new ServiceId($"network:{link.Client}->{link.Server}");
                var scheduler = new SimScheduler(_runtime, service);
                _links.Add(link, new SimLink(scheduler, this, link));
            }
        }

        public bool DebugPackets;

        public void Debug(string message) {
            if (DebugPackets)
            _runtime.Debug(message);
        }

        readonly Dictionary<LinkId, SimLink> _links = new Dictionary<LinkId, SimLink>(LinkId.Comparer);

        readonly Dictionary<SimEndpoint, SimSocket> _sockets = new Dictionary<SimEndpoint, SimSocket>(SimEndpoint.Comparer);


        public void InternalDeliver(SimEndpoint from, SimEndpoint to, object msg) {
            SimSocket socket;
            if (!_sockets.TryGetValue(to, out socket)) {
                // socket not bound

                _links[new LinkId(to.Machine, from.Machine)]
                    .Send(to, from, new IOException("Port not bound"));
                return;
            }
            
            socket.Deliver(msg, from);
        }

        public async Task<IConn> Listen(SimEnv proc, int port, TimeSpan timeout) {
            // socket is bound to the owner
            var endpoint = new SimEndpoint(proc.Id.Machine, port);

            if (!_sockets.TryGetValue(endpoint, out var socket)) {
                socket = new SimSocket(proc, endpoint);
                _sockets.Add(endpoint, socket);
            }

            var (msg, sender) = await socket.Read(timeout);

            var linkId = new LinkId(endpoint.Machine, sender.Machine);
            if (!_links.TryGetValue(linkId, out var link)) {
                throw new IOException($"Route not found {linkId}");
            }
            
            // msg == metadata
            
            return new SimConn(socket, sender, link);
        }

        public async Task<IConn> Connect(SimEnv process, SimEndpoint server) {
            var linkId = new LinkId(process.Id.Machine, server.Machine);
            if (!_links.TryGetValue(linkId, out var link)) {
                throw new IOException($"Route not found: {linkId}");
            }

            
            // todo: allocate port
            // todo: allow Azure SNAT delay scenario
            var clientEndpoint = new SimEndpoint(process.Id.Machine, 999);
            

            var clientSocket = new SimSocket(process, clientEndpoint);
            _sockets.Add(clientEndpoint, clientSocket);
            
            IConn conn = new SimConn(clientSocket, server, link);
            await conn.Write("OPEN");
            
            return conn;
        }
    }
    
    class SimConn : IConn {

        // maintain and send connection ID;
        readonly SimSocket _socket;
        readonly SimEndpoint _remote;
        readonly SimLink _link;
        
        public SimConn(SimSocket socket, SimEndpoint remote, SimLink link) {
            _socket = socket;
            _remote = remote;
            _link = link;
        }


        public Task Write(object message) {
            // we don't wait for the ACK.
            return _link.Send(_socket.Endpoint, _remote, message);
        }

        public async Task<object> Read(TimeSpan timeout) {

            var (msg, sender) = await _socket.Read(timeout);
            
            if (sender.ToString() != _remote.ToString()) {
                throw new IOException("Packet from unknown host");
            }
            _socket.Debug($"Receive {msg}");

            return msg;

        }

        public void Dispose() {
            // drop socket on dispose
        }
    }


    sealed class SimSocket {
        readonly SimEnv _env;
        public readonly SimEndpoint Endpoint;
        
        SimFuture<(object, SimEndpoint)> _pendingRead;

        readonly Queue<(object, SimEndpoint)> _incoming = new Queue<(object, SimEndpoint)>();

        public SimSocket(SimEnv env, SimEndpoint endpoint) {
            _env = env;
            Endpoint = endpoint;
        }

        public void Debug(string message) {
            _env.Debug(message);
        }

        public void Deliver(object msg, SimEndpoint client) {
            if (_pendingRead != null) {
                _pendingRead.SetResult((msg, client));
                _pendingRead = null;
            } else {
                _incoming.Enqueue((msg, client));
            }
        }

        public Task<(object, SimEndpoint)> Read(TimeSpan timeout) {
            if (_incoming.TryDequeue(out var tuple)) {
                return Task.FromResult(tuple);
            }

            _pendingRead = _env.Promise<(object, SimEndpoint)>(timeout, _env.Token);
            return _pendingRead.Task;
        }
        
        
        
        
    }

    sealed class SimEndpoint {
        public readonly string Machine;
        public readonly int Port;
        public SimEndpoint(string machine, int port) {
            Machine = machine;
            Port = port;
        }

        public override string ToString() {
            return $"{Machine}:{Port}";
        }

        public static IEqualityComparer<SimEndpoint> Comparer = new DelegateComparer<SimEndpoint>(a => a.ToString());
    }

    sealed class SimLink {
        readonly SimNetwork _network;
        readonly LinkId _link;
        SimScheduler _scheduler;
        readonly TaskFactory _factory;
        
        public void Debug(string l) {
            
            _network.Debug($"  {_link.Full,-34} {l}");
        }

        public SimLink(SimScheduler scheduler, SimNetwork network, LinkId link) {
            _scheduler = scheduler;
            _network = network;
            _link = link;
            _factory = new TaskFactory(_scheduler);
        }

        public Task Send(SimEndpoint client, SimEndpoint server, object msg) {
            // TODO: network cancellation
            _factory.StartNew(async () => {
                // delivery wait
                Debug($"Send {msg}");
                await SimDelayTask.Delay(50);
                
                _network.InternalDeliver(client, server, msg);
            });
            return Task.FromResult(true);
        }

       
    }
}