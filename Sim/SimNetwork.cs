using System;
using System.Collections.Generic;
using System.IO;
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
        

        public void SendPacket(SimPacket packet) {
            var routeId = new RouteId(packet.Source.Machine, packet.Destination.Machine);
            _routes[routeId].Send(packet);
        }


        public void InternalDeliver(SimPacket msg) {
            if (_runtime.ResolveHost(msg.Destination.Machine, out var machine)) {
                if (machine.TryDeliver(msg)) {
                    return; 
                }
                

            }
            
            var back = new SimPacket(msg.Destination, msg.Source, 
                new IOException("Connection refused"),
                0,
                SimFlag.Reset
            );
                
                
            SendPacket(back);
        }

        public bool TryGetRoute(string from, string to, out SimRoute r) {
            var linkId = new RouteId(from, to);
            return _routes.TryGetValue(linkId, out r);
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


    public sealed class SimEndpoint {
        public readonly string Machine;
        public readonly ushort Port;
        public SimEndpoint(string machine, ushort port) {
            Machine = machine;
            Port = port;
        }

        public override string ToString() {
            return $"{Machine}:{Port}";
        }

        public static readonly IEqualityComparer<SimEndpoint> Comparer = new DelegateComparer<SimEndpoint>(a => a.ToString());

        public static implicit operator SimEndpoint(string addr) {
            var args = addr.Split(":");
            return new SimEndpoint(args[0], ushort.Parse(args[1]));
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