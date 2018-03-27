using System;
using System.Threading;
using System.Threading.Tasks;

namespace SimMach.Sim {
    class SimControl  {
        readonly SimCluster _cluster;
        readonly SimRuntime _runtime;
        public SimControl(SimCluster cluster, SimRuntime runtime) {
            _cluster = cluster;
            _runtime = runtime;
        }

        public void StartServices(Predicate<ServiceId> selector = null) {
            _cluster.StartServices(selector);
        }

        public Task StopServices(Predicate<ServiceId> selector = null, TimeSpan? grace = null) {
            return _cluster.StopServices(selector, grace);
        }

        public void WipeStorage(string machine) {
            if (_cluster.ResolveHost(machine, out var simMachine)) {
                simMachine.WipeStorage();
            }
        }

        public void Debug(string message) {
            _runtime.Debug(message);
        }

        public Task Delay(TimeSpan i, CancellationToken token = default (CancellationToken)) {
            return SimDelayTask.Delay(i, token);
        }

        public TimeSpan Time => _runtime.Time;
    }
}