using System.Threading;
using System.Threading.Tasks;

namespace SimMach.Sim {
    class SimEnv : IEnv {
        
        readonly ServiceId Id;
        readonly SimRuntime Runtime;
        

        readonly CancellationTokenSource _cts;

        public CancellationToken Token => _cts.Token;
        public Task Delay(int i, CancellationToken token = default(CancellationToken)) {
            return SimFutureTask.Delay(i, token);
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

        
        public void Debug(string l) {
            Runtime.Debug($"  {Id.Machine,-13} {Id.Service,-20} {l}");
        }
       
    }
}