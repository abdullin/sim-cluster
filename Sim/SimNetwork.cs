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
                var debug = def.DebugRoutes.Contains(link);
                _routes.Add(link, new SimRoute(scheduler, this, link, debug));
            }
        }

        public bool DebugPackets;
        

        public void Debug(string message) {
            if (DebugPackets)
            _runtime.Debug(message);
        }

        readonly Dictionary<RouteId, SimRoute> _routes = new Dictionary<RouteId, SimRoute>(RouteId.Comparer);
        readonly Dictionary<SimEndpoint, SimSocket> _sockets = new Dictionary<SimEndpoint, SimSocket>(SimEndpoint.Comparer);
        

        public void SendPacket(SimPacket packet) {
            var routeId = new RouteId(packet.Source.Machine, packet.Destination.Machine);
            _routes[routeId].Send(packet);
        }


        public void InternalDeliver(SimPacket msg) {
            if (!_sockets.TryGetValue(msg.Destination, out var socket)) {
                // socket not bound
                
                var back = new SimPacket(msg.Destination, msg.Source, 
                    new IOException("Connection refused"),
                    0,
                    SimFlag.Reset
                    );
                
                
                SendPacket(back);
                return;
            }
             // https://eklitzke.org/how-tcp-sockets-work
            
            socket.Deliver(msg);
        }
        
        

        public async Task<ISocket> Bind(SimProc proc, int port, TimeSpan timeout) {
            // socket is bound to the owner
            var endpoint = new SimEndpoint(proc.Id.Machine, port);

            if (_sockets.ContainsKey(endpoint)) {
                throw new IOException($"Address {endpoint} in use");
            }

            var socket = new SimSocket(proc, endpoint, this);
            _sockets.Add(endpoint, socket);

            return socket;
        }

        int _socketId = 1000;

        public async Task<IConn> Connect(SimProc process, SimEndpoint server) {
            var linkId = new RouteId(process.Id.Machine, server.Machine);
            if (!_routes.TryGetValue(linkId, out var link)) {
                throw new IOException($"Route not found: {linkId}");
            }

            
            // todo: allocate port
            // todo: allow Azure SNAT delay scenario
            var clientEndpoint = new SimEndpoint(process.Id.Machine, _socketId ++);
            
            // do we have a real connection already?
            // if yes - then reuse
            

            var clientSocket = new SimSocket(process, clientEndpoint, this);
            _sockets.Add(clientEndpoint, clientSocket);

            
            var conn = new SimConn(clientSocket, server, process);
            
            clientSocket._connections.Add(server, conn);
            
            
            // handshake
            await conn.Write(null, SimFlag.Syn);
            
            var response = await conn.Read(5.Sec());
            if (response.Flag != (SimFlag.Ack | SimFlag.Syn)) {
                await conn.Write(null, SimFlag.Reset);
                clientSocket._connections.Remove(server);
                throw new IOException("Failed to connect");

            }
            await conn.Write(null, SimFlag.Ack);
            
            return new ClientConn(conn);
            
            
        }
    }

    [Flags]
    public enum SimFlag : byte {
        None = 0x00,
        Fin = 0x01,
        Reset = 0x04,
        Ack = 0x10,
        Syn = 0x02,
    }

    public class SimPacket {
        public readonly SimEndpoint Source;
        public readonly SimEndpoint Destination;
        
        public readonly object Payload;
        public readonly int SeqNumber;
        public readonly SimFlag Flag;

        public SimPacket(SimEndpoint source, SimEndpoint destination, object payload, int seqNumber, SimFlag flag) {
            Source = source;
            Destination = destination;
            Payload = payload;
            SeqNumber = seqNumber;
            Flag = flag;
        }

        public string Body() {
            var body = Payload == null ? "" : Payload.ToString();
            if (Flag != SimFlag.None) {
                body += $" {Flag.ToString().ToUpperInvariant()}";
            }

            return body.Trim();
        }
    }
    
    class SimConn  {

        // maintain and send connection ID;
        readonly SimSocket _socket;
        readonly SimEndpoint _remote;
        readonly SimProc _proc;
        int _sequence;
        
        SimFuture<SimPacket> _pendingRead;
        

        readonly Queue<SimPacket> _incoming = new Queue<SimPacket>();

        public SimConn(SimSocket socket, SimEndpoint remote, SimProc proc) {
            _socket = socket;
            _remote = remote;
            _proc = proc;
        }

        public async Task Write(object message, SimFlag flag = SimFlag.None) {
            if (_closed) {
                throw new IOException("Socket closed");
            }
            var packet = new SimPacket(_socket.Endpoint, _remote,  message, _sequence, flag);
            _socket.SendMessage(packet);
            _sequence++;
        }


        bool _closed;

        bool NextIsFin() {
            return _incoming.Count == 1 && _incoming.Peek().Flag == SimFlag.Fin;
        }


        void Close(string msg) {
            //_socket.Debug($"Close: {msg}");
            _closed = true;
        }


        public async Task<SimPacket> Read(TimeSpan timeout) {

            if (_closed) {
                throw new IOException("Socket closed");
            }

            if (_incoming.TryDequeue(out var tuple)) {

                if (tuple.Flag == SimFlag.Reset) {
                    Close("RESET");
                    throw new IOException("Connection reset");
                }

                if (NextIsFin()) {
                    Close("FIN");
                }

                return tuple;
            }

            _pendingRead = _proc.Promise<SimPacket>(timeout, _proc.Token);
            
            

            var msg = await _pendingRead.Task;

            if (msg.Flag == SimFlag.Reset) {
                Close("RESET");
                throw new IOException("Connection reset");
            }
            
            if (NextIsFin()) {
                Close("FIN");
            }
            
            return msg;

        }

        public void Dispose() {
            if (!_closed) {
                Write(null, SimFlag.Fin);
                Close("Dispose");
            }
            
            

            // drop socket on dispose
        }

        public void Deliver(SimPacket msg) {
            if (_pendingRead != null) {
                _pendingRead.SetResult(msg);
                _pendingRead = null;
            } else {
                _incoming.Enqueue(msg);
            }
        }
    }
    
    sealed class ClientConn : IConn {
        public ClientConn(SimConn conn) {
            _conn = conn;
        }

        readonly SimConn _conn;
        
        public void Dispose() {
            _conn.Dispose();
        }

        public async Task Write(object message) {
            await _conn.Write(message);
        }

        public async Task<object> Read(TimeSpan timeout) {
            try {
                var msg = await _conn.Read(timeout);
                return msg.Payload;
            } catch (TimeoutException ex) {
                throw new IOException("Read timeout", ex);
            }
        }
    }


    public interface ISocket : IDisposable {
        Task<IConn> Accept();
    }

    



    sealed class SimSocket : ISocket {
        readonly SimProc _proc;
        public readonly SimEndpoint Endpoint;
        readonly SimNetwork _net;
        


        public readonly Dictionary<SimEndpoint, SimConn> _connections =
            new Dictionary<SimEndpoint, SimConn>(SimEndpoint.Comparer);

        readonly Queue<IConn> _incoming = new Queue<IConn>();

        SimFuture<IConn> _poll;

        public SimSocket(SimProc proc, SimEndpoint endpoint, SimNetwork net) {
            _proc = proc;
            Endpoint = endpoint;
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

        public void Deliver(SimPacket msg) {
            
            
            if (_connections.TryGetValue(msg.Source, out var conn)) {
                conn.Deliver(msg);
                return;
            }

            if (msg.Flag != SimFlag.Syn) {
                Debug("Non-SYN packet was dropped");
                return;
            }
            
            conn = new SimConn(this, msg.Source, _proc);
            _connections.Add(msg.Source, conn);
            
            _proc.Schedule(async () => {
                await conn.Write(null, SimFlag.Ack | SimFlag.Syn);
                var resp = await conn.Read(5.Sec());
                if (resp.Flag != SimFlag.Ack) {
                    Debug("Non ACK packet received");
                    await conn.Write(null, SimFlag.Reset);
                    _connections.Remove(msg.Source);
                    return;
                }
                
                var ready = new ClientConn(conn);
            
                if (_poll != null) {
                    _poll.SetResult(ready);
                    _poll = null;
                } else {
                    _incoming.Enqueue(ready);    
                }
            });
        }

        public void Dispose() { }

        public void SendMessage(SimPacket message) {
            _net.SendPacket(message);
        }
    }

    public sealed class SimEndpoint {
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

        public static implicit operator SimEndpoint(string addr) {
            var args = addr.Split(":");
            return new SimEndpoint(args[0], int.Parse(args[1]));
        }
    }

    sealed class SimRoute {
        readonly SimNetwork _network;
        readonly RouteId _route;
        SimScheduler _scheduler;
        readonly TaskFactory _factory;
        readonly bool _debug;

       void Debug(string l) {
           if (_debug)
            _network.Debug($"  {_route.Full,-34} {l}");
        }

        public SimRoute(SimScheduler scheduler, SimNetwork network, RouteId route, bool debug) {
            _scheduler = scheduler;
            _network = network;
            _route = route;
            _factory = new TaskFactory(_scheduler);
            _debug = debug;
        }

        public Task Send(SimPacket msg) {
            Debug($"Send {msg.Body()}");
            // TODO: network cancellation
            _factory.StartNew(async () => {
                // delivery wait
                try {
                    await SimDelayTask.Delay(50);
                    _network.InternalDeliver(msg);
                } catch (Exception ex) {
                    Debug($"FATAL: {ex}");
                }
            });
            return Task.FromResult(true);
        }
    }
}