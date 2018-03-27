using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;

namespace SimMach.Sim {

  
    

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

        public override string ToString() {
            return $"{Source}->{Destination}: {Body()}";
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
        public readonly SimEndpoint RemoteAddress;
        readonly SimProc _proc;
        int _sequence;
        
        SimFuture<SimPacket> _pendingRead;
        

        readonly Queue<SimPacket> _incoming = new Queue<SimPacket>();

        public SimConn(SimSocket socket, SimEndpoint remoteAddress, SimProc proc) {
            _socket = socket;
            RemoteAddress = remoteAddress;
            _proc = proc;
        }

        public async Task Write(object message, SimFlag flag = SimFlag.None) {
            if (_closed) {
                throw new IOException("Socket closed");
            }
            var packet = new SimPacket(_socket.Endpoint, RemoteAddress,  message, _sequence, flag);
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

        public SimEndpoint RemoteAddress => _conn.RemoteAddress;
        
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
        readonly SimCluster _network;
        readonly RouteId _route;
        SimScheduler _scheduler;
        readonly TaskFactory _factory;
        readonly RouteDef _def;

       void Debug(string l) {
           if (_def.Debug)
            _network.Debug($"  {_route.Full,-34} {l}");
        }

        public SimRoute(SimScheduler scheduler, SimCluster network, RouteId route, RouteDef def) {
            _scheduler = scheduler;
            _network = network;
            _route = route;
            _factory = new TaskFactory(_scheduler);
            _def = def;
        }

        public Task Send(SimPacket msg) {
            Debug($"Send {msg.Body()}");
            // TODO: network cancellation
            _factory.StartNew(async () => {
                // delivery wait
                try {
                    var latency = _def.Latency(_network.Rand);
                    await SimDelayTask.Delay(latency);
                    _network.InternalDeliver(msg);
                } catch (Exception ex) {
                    Debug($"FATAL: {ex}");
                }
            });
            return Task.FromResult(true);
        }
    }
}