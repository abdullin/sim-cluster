using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace SimMach.Sim {
    public class SimControl  {
        public readonly SimCluster Cluster;
        readonly SimRuntime _runtime;

        public SimRandom Rand => Cluster.Rand;
        
        public SimControl(SimCluster cluster, SimRuntime runtime) {
            Cluster = cluster;
            _runtime = runtime;
        }

        public void StartServices(Predicate<ServiceId> selector = null) {
            Cluster.StartServices(selector);
        }

        public Task StopServices(Predicate<ServiceId> selector = null, TimeSpan? grace = null) {
            return Cluster.StopServices(selector, grace);
        }

        public void WipeStorage(string machine) {
            if (Cluster.ResolveHost(machine, out var simMachine)) {
                simMachine.WipeStorage();
            }
        }

        public void Debug(LogType type, string message) {
            _runtime.Debug(type, message);
        }

        public Task Delay(TimeSpan i, CancellationToken token = default (CancellationToken)) {
            return SimDelayTask.Delay(i, token);
        }

        public TimeSpan Time => _runtime.Time;
    }
}