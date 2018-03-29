using System;
using System.Collections.Generic;
using System.Diagnostics;
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
        public readonly uint SeqNumber;
        public readonly uint AckNumber;
        public readonly SimFlag Flag;

        public SimPacket(SimEndpoint source, SimEndpoint destination, object payload, 
            uint seqNumber,
            uint ackNumber,
            SimFlag flag) {
            Source = source;
            Destination = destination;
            Payload = payload;
            SeqNumber = seqNumber;
            AckNumber = ackNumber;
            Flag = flag;
        }

        public uint NextSeqNumber => SeqNumber + 1;
        
        

        public override string ToString() {
            return $"{Source}->{Destination}: {BodyString()}";
        }

        public string BodyString() {
            var body = Payload == null ? "" : Payload.ToString();
            if (Flag != SimFlag.None) {
                body += $" {Flag.ToString().ToUpperInvariant()}";
            }

            return body.Trim();
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

    public sealed class SimRoute {
        readonly SimCluster _network;
        readonly RouteId _route;
        SimScheduler _scheduler;
        readonly TaskFactory _factory;
        readonly RouteDef _def;

       void Debug(SimPacket msg, string l) {
           if (_def.Debug(msg)) {
               var route = $"{msg.Source}->{msg.Destination}";
               _network.Debug($"  {route,-34} {l}");
           }
       }

        public SimRoute(SimScheduler scheduler, SimCluster network, RouteId route, RouteDef def) {
            _scheduler = scheduler;
            _network = network;
            _route = route;
            _factory = new TaskFactory(_scheduler);
            _def = def;
        }

        public Task Send(SimPacket msg) {

            
         
            Debug(msg, $"Send {msg.BodyString()}");
            // TODO: network cancellation
            _factory.StartNew(async () => {
                // delivery wait
                try {
                    var latency = _def.Latency(_network.Rand);
                    await SimDelayTask.Delay(latency);
                    _network.InternalDeliver(msg);
                } catch (Exception ex) {
                    Debug(msg, $"FATAL: {ex}");
                }
            });
            return Task.FromResult(true);
        }
    }
}