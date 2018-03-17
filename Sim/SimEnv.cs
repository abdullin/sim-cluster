using System;
using System.Threading;
using System.Threading.Tasks;

namespace SimMach.Sim {
    class SimEnv : IEnv {
        
        public readonly ServiceId Id;
        readonly SimRuntime Runtime;
        

        readonly CancellationTokenSource _cts;

        public CancellationToken Token => _cts.Token;
        public Task Delay(int i, CancellationToken token ) {
            return SimDelayTask.Delay(i, token);
        }
        public Task Delay(TimeSpan i, CancellationToken token) {
            return SimDelayTask.Delay(i, token);
        }

        public SimCompletionSource<T> Promise<T>(TimeSpan timeout, CancellationToken token) {
            return new SimCompletionSource<T>(timeout, token);
        }

        public async Task SimulateWork(int ms, CancellationToken token) {
            Runtime.RecordActivity();
            await SimDelayTask.Delay(ms, token);
            Runtime.RecordActivity();
        }

        public SimEnv(ServiceId id, SimRuntime runtime) {
            _cts = new CancellationTokenSource();
            Id = id;
            Runtime = runtime;
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
        
        public Task<IConn> Connect(string endpoint, int port) {
            var server = new SimEndpoint(endpoint, port);
            return Runtime.Connect(this, server);
        }

        public Task<IConn> Listen(int port) {
            return Runtime.Listen(this, port);
        }


        public void Debug(string l) {
            Runtime.Debug($"  {Id.Machine,-13} {Id.Service,-20} {l}");
        }
       
    }
}