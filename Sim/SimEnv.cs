using System;
using System.Threading;
using System.Threading.Tasks;

namespace SimMach.Sim {
    class SimEnv : IEnv {
        
        readonly ServiceId Id;
        readonly SimRuntime Runtime;
        

        readonly CancellationTokenSource _cts;

        public CancellationToken Token => _cts.Token;
        public Task Delay(int i, CancellationToken token ) {
            return SimFutureTask.Delay(i, token);
        }
        public Task Delay(TimeSpan i, CancellationToken token) {
            return SimFutureTask.Delay(i, token);
        }

        public async Task SimulateWork(int ms, CancellationToken token) {
            Runtime.RecordActivity();
            await SimFutureTask.Delay(ms, token);
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

        
        public void Debug(string l) {
            Runtime.Debug($"  {Id.Machine,-13} {Id.Service,-20} {l}");
        }
       
    }
}