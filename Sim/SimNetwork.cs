using System;
using System.Collections.Generic;
using System.IO;
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
                _routes.Add(link, new SimRoute(scheduler, this, link));
            }
        }

        public bool DebugPackets;

        public void Debug(string message) {
            if (DebugPackets)
            _runtime.Debug(message);
        }

        public TracePoint Trace(string name) {
            return _runtime.Tracer.Scope(0, name, "net");
        }

        readonly Dictionary<RouteId, SimRoute> _routes = new Dictionary<RouteId, SimRoute>(RouteId.Comparer);

        readonly Dictionary<SimEndpoint, SimSocket> _sockets = new Dictionary<SimEndpoint, SimSocket>(SimEndpoint.Comparer);

        public void SendPacket(SimEndpoint from, SimEndpoint to, object message) {
            _routes[new RouteId(from.Machine, to.Machine)].Send(from, to, message);
        }


        public void InternalDeliver(SimEndpoint from, SimEndpoint to, object msg) {
            if (!_sockets.TryGetValue(to, out var socket)) {
                // socket not bound
                SendPacket(from, to, new IOException("Socket not found"));
                return;
            }
             // https://eklitzke.org/how-tcp-sockets-work
            
            socket.Deliver(msg, from);
        }
        
        

        public async Task<ISocket> Listen(SimProc proc, int port, TimeSpan timeout) {
            // socket is bound to the owner
            var endpoint = new SimEndpoint(proc.Id.Machine, port);

            if (_sockets.ContainsKey(endpoint)) {
                throw new IOException($"Address {endpoint} in use");
            }

            var socket = new SimSocket(proc, endpoint, this);
            _sockets.Add(endpoint, socket);

            return socket;
        }

        public async Task<IConn> Connect(SimProc process, SimEndpoint server) {
            var linkId = new RouteId(process.Id.Machine, server.Machine);
            if (!_routes.TryGetValue(linkId, out var link)) {
                throw new IOException($"Route not found: {linkId}");
            }

            
            // todo: allocate port
            // todo: allow Azure SNAT delay scenario
            var clientEndpoint = new SimEndpoint(process.Id.Machine, 999);
            
            // do we have a real connection already?
            // if yes - then reuse
            

            var clientSocket = new SimSocket(process, clientEndpoint, this);
            _sockets.Add(clientEndpoint, clientSocket);

            
            var conn = new SimConn(clientSocket, server, process);
            
            clientSocket._connections.Add(server, conn);
            
            
            // handshake
            await conn.Write("SYN");
            await conn.Read(5.Sec());
            await conn.Write("ACK");
            
            return new ClientConn(conn, process);
            
            
        }
    }
    
    class SimConn : IConn {

        // maintain and send connection ID;
        readonly SimSocket _socket;
        readonly SimEndpoint _remote;
        readonly SimProc _proc;
        
        
        SimFuture<object> _pendingRead;
        

        readonly Queue<object> _incoming = new Queue<object>();

        public SimConn(SimSocket socket, SimEndpoint remote, SimProc proc) {
            _socket = socket;
            _remote = remote;
            _proc = proc;
        }


        public async Task Write(object message) {
            if (_closed) {
                throw new IOException("Socket closed");
            }
            // we don't wait for the ACK.
            // but add latency for putting on the wire
            //await _env.Delay(1.Ms(), _env.Token);

            _socket.SendMessage(_remote, message);
        }


        bool _closed;

        bool NextIsFin() {
            return _incoming.Count == 1 && _incoming.Peek() == SimTcp.FIN;
        }


        void Close(string msg) {
            //_socket.Debug($"Close: {msg}");
            _closed = true;
        }

        public async Task<object> Read(TimeSpan timeout) {

            if (_closed) {
                throw new IOException("Socket closed");
            }



            if (_incoming.TryDequeue(out var tuple)) {

                if (NextIsFin()) {
                    Close(SimTcp.FIN);
                }

                return tuple;
            }

            _pendingRead = _proc.Promise<object>(timeout, _proc.Token);


            var msg = await _pendingRead.Task;

            //_socket.Debug($"Receive {msg}");

            if (NextIsFin()) {
                Close("FIN");
            }

            return msg;

        }

        public void Dispose() {
            if (!_closed) {
                _socket.SendMessage(_remote, SimTcp.FIN);
                Close("Dispose");
            }
            
            

            // drop socket on dispose
        }

        public void Deliver(object msg) {
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

    public static class SimTcp {
        public static readonly string SYN = "SYN";
        public static readonly string FIN = "FIN";
        public static readonly string ACK = "ACK";
        public static readonly string SYN_ACK = "SYN_ACK";
    }




    sealed class SimSocket : ISocket {
        readonly SimProc _proc;
        readonly SimEndpoint _endpoint;
        readonly SimNetwork _net;
        


        public readonly Dictionary<SimEndpoint, SimConn> _connections =
            new Dictionary<SimEndpoint, SimConn>(SimEndpoint.Comparer);

        readonly Queue<IConn> _incoming = new Queue<IConn>();

        SimFuture<IConn> _poll;

        public SimSocket(SimProc proc, SimEndpoint endpoint, SimNetwork net) {
            _proc = proc;
            _endpoint = endpoint;
            _net = net;
        }

        public void Debug(string message) {
            _proc.Debug(message);
        }

        public Task<IConn> Accept() {
            if (_incoming.TryDequeue(out var conn)) {
                return Task.FromResult<IConn>(conn);
            }

            if (_poll != null) {
                throw new IOException("There is a wait already");
            }

            _poll = _proc.Promise<IConn>(Timeout.InfiniteTimeSpan, _proc.Token);
            return _poll.Task;
        }

        public void Deliver(object msg, SimEndpoint client) {
            
            if (_connections.TryGetValue(client, out var conn)) {
                conn.Deliver(msg);
                return;
            }

            if (msg != SimTcp.SYN) {
                Debug("Non-SYN packet was dropped");
                return;
            }
            
            
            
            conn = new SimConn(this, client, _proc);
            _connections.Add(client, conn);
            
            _proc.Schedule(async () => {
                await conn.Write(SimTcp.SYN_ACK);
                if (SimTcp.ACK != await conn.Read(5.Sec())) {
                    Debug("Non ACK packet received");
                    _connections.Remove(client);
                    return;
                }
                
                var ready = new ClientConn(conn, _proc);
            
                if (_poll != null) {
                    _poll.SetResult(ready);
                    _poll = null;
                } else {
                    _incoming.Enqueue(ready);    
                }
            });
        }

        public void Dispose() { }

        public void SendMessage(SimEndpoint remote, object message) {
            _net.SendPacket(_endpoint, remote, message);
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

        public static readonly IEqualityComparer<SimEndpoint> Comparer = new DelegateComparer<SimEndpoint>(a => a.ToString());
    }

    sealed class SimRoute {
        readonly SimNetwork _network;
        readonly RouteId _route;
        SimScheduler _scheduler;
        readonly TaskFactory _factory;
        
        public void Debug(string l) {
            
            _network.Debug($"  {_route.Full,-34} {l}");
        }

        public SimRoute(SimScheduler scheduler, SimNetwork network, RouteId route) {
            _scheduler = scheduler;
            _network = network;
            _route = route;
            _factory = new TaskFactory(_scheduler);
        }

        public Task Send(SimEndpoint client, SimEndpoint server, object msg) {
            var text = $"{msg ?? "null"}";
            Debug("Send " + text);
            // TODO: network cancellation
            _factory.StartNew(async () => {
                // delivery wait
            
                    await SimDelayTask.Delay(50);
                    _network.InternalDeliver(client, server, msg);
            
            });
            return Task.FromResult(true);
        }
    }
}