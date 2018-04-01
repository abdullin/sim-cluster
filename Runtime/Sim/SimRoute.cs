using System;
using System.Threading.Tasks;

namespace SimMach.Sim {
    public sealed class SimRoute {
        readonly SimCluster _network;
        readonly RouteId _route;
        readonly SimScheduler _scheduler;
        readonly TaskFactory _factory;
        readonly RouteDef _def;

        void Debug(LogType type, SimPacket msg,  string l, bool force= false) {
            if (force || _def.Debug(msg)) {
                var route = $"{msg.Source}->{msg.Destination}";
                _network.Debug(type, $"  {route,-34} {l}");
            }
        }

        public SimRoute(SimScheduler scheduler, SimCluster network, RouteId route, RouteDef def) {
            _scheduler = scheduler;
            _network = network;
            _route = route;
            _factory = new TaskFactory(_scheduler);
            _def = def;
        }

        public void Kill() {
            //_network.Runtime.FutureQueue.Erase(_scheduler);
        }

        public Task Send(SimPacket msg) {
            

            if (_def.PacketLoss != null && _def.PacketLoss(_network.Rand)) {
                    Debug(LogType.Fault,  msg, $"LOST {msg.BodyString()}", _def.LogFaults);
                // we just lost a packet.
                return Task.FromResult(true);
            }
            Debug(LogType.Info, msg, $"Send {msg.BodyString()}");
            
            // TODO: network cancellation
            _factory.StartNew(async () => {
                // delivery wait
                try {
                    var latency = _def.Latency(_network.Rand);
                    await SimDelayTask.Delay(latency);
                    _network.InternalDeliver(msg);
                } catch (Exception ex) {
                    Debug(LogType.Error,  msg, $"FATAL: {ex}");
                }
            });
            return Task.FromResult(true);
        }
    }
}