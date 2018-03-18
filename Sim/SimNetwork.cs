using System;
using System.Collections.Generic;
using System.IO;
using System.Net.Sockets;
using System.Threading;
using System.Threading.Tasks;
using System.Xml.Schema;

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

        public void SendPacket(SimEndpoint from, SimEndpoint to, object message) {
            _links[new LinkId(from.Machine, to.Machine)]
                .Send(from, to, message);
        }


        public void InternalDeliver(SimEndpoint from, SimEndpoint to, object msg) {
            if (!_sockets.TryGetValue(to, out var socket)) {
                // socket not bound
                SendPacket(from, to, new IOException("Socket not found"));
                return;
            }
             // https://eklitzke.org/how-tcp-sockets-work
            
            socket.Deliver(msg, from, this);
        }
        
        

        public async Task<ISocket> Listen(SimEnv proc, int port, TimeSpan timeout) {
            // socket is bound to the owner
            var endpoint = new SimEndpoint(proc.Id.Machine, port);

            if (_sockets.ContainsKey(endpoint)) {
                throw new IOException($"Address {endpoint} in use");
            }

            var socket = new SimSocket(proc, endpoint);
            _sockets.Add(endpoint, socket);

            return socket;
        }

        public async Task<IConn> Connect(SimEnv process, SimEndpoint server) {
            var linkId = new LinkId(process.Id.Machine, server.Machine);
            if (!_links.TryGetValue(linkId, out var link)) {
                throw new IOException($"Route not found: {linkId}");
            }

            
            // todo: allocate port
            // todo: allow Azure SNAT delay scenario
            var clientEndpoint = new SimEndpoint(process.Id.Machine, 999);
            
            // do we have a real connection already?
            // if yes - then reuse
            

            var clientSocket = new SimSocket(process, clientEndpoint);
            _sockets.Add(clientEndpoint, clientSocket);
            
            var conn = new SimConn(clientSocket, server, this, process);
            
            clientSocket._connections.Add(server, conn);
            
            await conn.Write("SYN");
            
            return conn;
        }
    }
    
    class SimConn : IConn {

        // maintain and send connection ID;
        readonly SimSocket _socket;
        readonly SimEndpoint _remote;
        readonly SimNetwork _link;
        readonly SimEnv _env;
        
        
        SimFuture<object> _pendingRead;
        

        readonly Queue<object> _incoming = new Queue<object>();


        static readonly object FIN = "FIN";
        
        public SimConn(SimSocket socket, SimEndpoint remote, SimNetwork link, SimEnv env) {
            _socket = socket;
            _remote = remote;
            _link = link;
            _env = env;
        }


        public async Task Write(object message) {
            if (_closed) {
                throw new IOException("Socket closed");
            }
            // we don't wait for the ACK.
            _link.SendPacket(_socket.Endpoint, _remote, message);
        }


        bool _closed;
        

        public async Task<object> Read(TimeSpan timeout) {

            if (_closed) {
                throw new IOException("Socket closed");
            }
            
            if (_incoming.TryDequeue(out var tuple)) {
                return tuple;
            }

            _pendingRead = _env.Promise<object>(timeout, _env.Token);
            var msg = await _pendingRead.Task;

            
            _socket.Debug($"Receive {msg}");
            return msg;
        }

        public void Dispose() {
            if (!_closed) {
                _link.SendPacket(_socket.Endpoint, _remote, FIN);
                _closed = true;
            }

            // drop socket on dispose
        }

        public void Deliver(object msg) {
            if (msg == FIN) {
                _closed = true;
                return;
            }
            if (_pendingRead != null) {
                _pendingRead.SetResult(msg);
                _pendingRead = null;
            } else {
                _incoming.Enqueue(msg);
            }
        }
    }

    public interface ISocket : IDisposable {
        Task<IConn> Accept();
    }




    sealed class SimSocket : ISocket {
        readonly SimEnv _env;
        public readonly SimEndpoint Endpoint;
        

        public readonly Dictionary<SimEndpoint, SimConn> _connections =
            new Dictionary<SimEndpoint, SimConn>(SimEndpoint.Comparer);

        readonly Queue<SimConn> _incoming = new Queue<SimConn>();

        SimFuture<IConn> _poll;

        public SimSocket(SimEnv env, SimEndpoint endpoint) {
            _env = env;
            Endpoint = endpoint;
        }

        public void Debug(string message) {
            _env.Debug(message);
        }

        public Task<IConn> Accept() {
            if (_incoming.TryDequeue(out var conn)) {
                return Task.FromResult<IConn>(conn);
            }

            if (_poll != null) {
                throw new IOException("There is a wait already");
            }

            _poll = _env.Promise<IConn>(Timeout.InfiniteTimeSpan, _env.Token);
            return _poll.Task;
        }

        public void Deliver(object msg, SimEndpoint client, SimNetwork link) {
            
            if (_connections.TryGetValue(client, out var conn)) {
                conn.Deliver(msg);
                return;
            }
            
            conn = new SimConn(this, client, link,_env);
            
            _connections.Add(client, conn);
           

            if (_poll != null) {
                _poll.SetResult(conn);
                _poll = null;
            } else {
                _incoming.Enqueue(conn);    
            }
        }

        public void Dispose() { }
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