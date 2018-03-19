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

        public TracePoint Trace(SimEndpoint client, SimEndpoint server, SimPacket pack) {
            return TracePoint.None;
            //return _runtime.Tracer.TracePacket(client, server, pack);
        }

        readonly Dictionary<RouteId, SimRoute> _routes = new Dictionary<RouteId, SimRoute>(RouteId.Comparer);
        readonly Dictionary<SimEndpoint, SimSocket> _sockets = new Dictionary<SimEndpoint, SimSocket>(SimEndpoint.Comparer);
        

        public void SendPacket(SimEndpoint from, SimEndpoint to, SimPacket message) {
            _routes[new RouteId(from.Machine, to.Machine)].Send(from, to, message);
        }


        public void InternalDeliver(SimEndpoint from, SimEndpoint to, SimPacket msg) {
            if (!_sockets.TryGetValue(to, out var socket)) {
                // socket not bound
                SendPacket(from, to, new SimPacket(new IOException("Socket not found"),0));
                return;
            }
             // https://eklitzke.org/how-tcp-sockets-work
            
            socket.Deliver(msg, from);
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
            await conn.Write("SYN");
            await conn.Read(5.Sec());
            await conn.Write("ACK");
            
            return new ClientConn(conn, process);
            
            
        }
    }

    public class SimPacket {
        public readonly object Payload;
        public readonly int Seq;

        public SimPacket(object payload, int seq) {
            Payload = payload;
            Seq = seq;
        }
    }
    
    class SimConn  {

        // maintain and send connection ID;
        public readonly SimSocket Socket;
        public readonly SimEndpoint Remote;
        
        readonly SimProc _proc;


        int _sequence;
        
        SimFuture<SimPacket> _pendingRead;
        

        readonly Queue<SimPacket> _incoming = new Queue<SimPacket>();

        public SimConn(SimSocket socket, SimEndpoint remote, SimProc proc) {
            Socket = socket;
            Remote = remote;
            _proc = proc;
        }

        public int OutgoingSequence => _sequence;
        public int LastAck => _ackSequence;

        int _ackSequence;

        public async Task Write(object message) {
            if (_closed) {
                throw new IOException("Socket closed");
            }
            // we don't wait for the ACK.
            // but add latency for putting on the wire
            //await _env.Delay(1.Ms(), _env.Token);
            
            var id = $"{Socket.Endpoint}->{Remote}|{OutgoingSequence}";

            using (_proc.TraceScope("Write")) {
                await _proc.Delay(1, _proc.Token);
                _proc.FlowStart(message.ToString(), id.GetHashCode().ToString());
            }

            Socket.SendMessage(Remote, new SimPacket(message, _sequence));
            _sequence++;
        }


        bool _closed;

        bool NextIsFin() {
            return _incoming.Count == 1 && _incoming.Peek().Payload == SimTcp.FIN;
        }


        void Close(string msg) {
            //_socket.Debug($"Close: {msg}");
            _closed = true;
        }

        public async Task TraceRead(SimPacket msg) {
            var id = $"{Remote}->{Socket.Endpoint}|{msg.Seq}";
            _proc.FlowEnd(msg.Payload.ToString(), id.GetHashCode().ToString());
            using (_proc.TraceScope("Read")) {
                await _proc.Delay(1, _proc.Token);
            }
        }

        public async Task<object> Read(TimeSpan timeout) {

            if (_closed) {
                throw new IOException("Socket closed");
            }



            if (_incoming.TryDequeue(out var tuple)) {

                if (NextIsFin()) {
                    Close(SimTcp.FIN);
                }

                await TraceRead(tuple);

                _ackSequence = tuple.Seq;

                return tuple.Payload;
            }

            _pendingRead = _proc.Promise<SimPacket>(timeout, _proc.Token);


            var msg = await _pendingRead.Task;
            _ackSequence = msg.Seq;

            //_socket.Debug($"Receive {msg}");

            if (NextIsFin()) {
                Close("FIN");
            }
            
            await TraceRead(msg);

            return msg.Payload;

        }

        public void Dispose() {
            if (!_closed) {
                Write(SimTcp.FIN);
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
        public ClientConn(SimConn conn, SimProc proc) {
            _conn = conn;
            _proc = proc;
        }

        readonly SimConn _conn;
        readonly SimProc _proc;
        public void Dispose() {
            _conn.Dispose();
        }

        public async Task Write(object message) {
            

          
            await _conn.Write(message);
        }

        public async Task<object> Read(TimeSpan timeout) {
            return await _conn.Read(timeout);
            

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

        public void Deliver(SimPacket msg, SimEndpoint client) {
            
            if (_connections.TryGetValue(client, out var conn)) {
                conn.Deliver(msg);
                return;
            }

            if (msg.Payload != SimTcp.SYN) {
                Debug("Non-SYN packet was dropped");
                return;
            }
            
            
            
            conn = new SimConn(this, client, _proc);
            conn.TraceRead(msg);
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

        public void SendMessage(SimEndpoint remote, SimPacket message) {
            _net.SendPacket(Endpoint, remote, message);
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

        public Task Send(SimEndpoint client, SimEndpoint server, SimPacket msg) {
            var text = $"{msg.Payload ?? "null"}";
            Debug("Send " + text);
            // TODO: network cancellation
            _factory.StartNew(async () => {
                using (_network.Trace(client, server, msg)) {
                    // delivery wait


                    await SimDelayTask.Delay(50);
                    _network.InternalDeliver(client, server, msg);
                }

            });
            return Task.FromResult(true);
        }
    }
}