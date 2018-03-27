using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;

namespace SimMach.Sim {
    sealed class SimCluster : IDisposable {
        
        readonly SimRuntime _runtime;
        
        public readonly Dictionary<string, SimMachine> Machines = new Dictionary<string, SimMachine>();
        public readonly Dictionary<RouteId, SimRoute> Routes = new Dictionary<RouteId, SimRoute>(RouteId.Comparer);
        public readonly SimRandom Rand;
        
        public SimCluster(ClusterDef cluster, SimRuntime runtime) {
            _runtime = runtime;
            Rand = _runtime.Rand;

            // we register each link as a network service
            foreach (var (id, def) in cluster.Routes) {
                var service = new ServiceId($"network:{id.Source}->{id.Destinaton}");
                var scheduler = new SimScheduler(_runtime, service);
                Routes.Add(id, new SimRoute(scheduler, this, id, def));
            }
            
            foreach (var machine in cluster.Services.GroupBy(i => i.Key.Machine)) {
                var m = new SimMachine(machine.Key, runtime, this);

                foreach (var pair in machine) {
                    m.Install(pair.Key, pair.Value);
                }
                Machines.Add(machine.Key, m);
            }
        }
        
        public bool ResolveHost(string name, out SimMachine m) {
            return Machines.TryGetValue(name, out m);
        }

        public void Debug(string message) {
            _runtime.Debug(message);
        }

        public void SendPacket(SimPacket packet) {
            var source = packet.Source.Machine;
            var destination = packet.Destination.Machine;
            var routeId = new RouteId(GetNetwork(source), GetNetwork(destination));
            Routes[routeId].Send(packet);
        }

        public void InternalDeliver(SimPacket msg) {
            if (ResolveHost(msg.Destination.Machine, out var machine)) {
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

        static string GetNetwork(string machine) {
            if (machine.IndexOf('.') < 0) {
                return machine;
            }

            return string.Join('.', machine.Split('.').Skip(1));
        }
        
        public IEnumerable<SimService> Filter(Predicate<ServiceId> filter) {
            if (null == filter) {
                return Machines.SelectMany(p => p.Value.Services.Values);
            }

            return Machines.SelectMany(p => p.Value.Services.Values).Where(p => filter(p.Id));
        }
        
        public void StartServices(Predicate<ServiceId> selector = null) {
            var services = Filter(selector).ToArray();
            if (services.Length == 0) {
                throw new ArgumentException("No services match selector", nameof(selector));
            }
            
            foreach (var svc in services) {
                svc.Launch(ex => {
                    if (ex != null) {
                        _runtime.Halt($"'{svc.Id.Full}' faulted", ex);
                    }
                });
            }
        }

        public Task StopServices(Predicate<ServiceId> selector = null, TimeSpan? grace = null) {
            var tasks = Filter(selector).Select(p => p.Stop(grace ?? 2.Sec())).ToArray();
            if (tasks.Length == 0) {
                throw new ArgumentException("No services match selector", nameof(selector));
            }
            
            return Task.WhenAll(tasks);
        }
        
        public bool TryGetRoute(string from, string to, out SimRoute r) {
            var linkId = new RouteId(GetNetwork(from), GetNetwork(to));
            return Routes.TryGetValue(linkId, out r);
        }

        public void Dispose() {
            foreach (var (_, machine) in Machines) {
                foreach (var (_, svc) in machine.Services) {
                    svc.ReleaseResources();
                }
            }
        }
    }
}