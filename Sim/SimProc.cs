using System;
using System.Threading;
using System.Threading.Tasks;

namespace SimMach.Sim {



    class SimProc : IEnv {
        readonly int _procId;
        readonly TaskFactory _scheduler;
        public readonly ServiceId Id;
        readonly SimRuntime Runtime;


        readonly CancellationTokenSource _cts;

        public CancellationToken Token => _cts.Token;







        public Task Delay(int i, CancellationToken token) {
            return SimDelayTask.Delay(i, token);
        }

        public Task Delay(TimeSpan i, CancellationToken token) {
            return SimDelayTask.Delay(i, token);
        }

        public SimFuture<T> Promise<T>(TimeSpan timeout, CancellationToken token) {
            return new SimFuture<T>(timeout, token);
        }

        public async Task SimulateWork(string name, TimeSpan ms, CancellationToken token) {
            Runtime.RecordActivity();
            await SimDelayTask.Delay(ms, token);
            Runtime.RecordActivity();
        }

        public SimProc(ServiceId id, SimRuntime runtime, int procId, TaskFactory scheduler) {
            _cts = new CancellationTokenSource();
            Id = id;
            Runtime = runtime;
            _procId = procId;
            _scheduler = scheduler;
        }

        public void Schedule(Func<Task> action) {
            _scheduler.StartNew(action);
        }

        public void Cancel() {
            // issues a soft cancel token
            _cts.Cancel();
        }

        bool _killed;

        public void Kill() {
            _killed = true;
        }

        public TimeSpan Time => Runtime.Time;

        public async Task<IConn> Connect(string endpoint, int port) {
            var server = new SimEndpoint(endpoint, port);

            return await Runtime.Connect(this, server);

        }

        public async Task<ISocket> Listen(int port, TimeSpan timeout) {
            return await Runtime.Bind(this, port, timeout);
        }


        public void Debug(string l) {
            Runtime.Debug($"  {Id.Machine,-13} {Id.Service,-20} {l}");
        }
    }
}