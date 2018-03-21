using System;
using System.Threading;
using System.Threading.Tasks;
using SimMach.Sim;

namespace SimMach.Sim {
    
    
    class SimService {
        readonly ServiceId _id;
        readonly Func<SimProc, Task> _launcher;
        
        Task _task;
        SimProc _proc;
        
        
        readonly SimScheduler _scheduler;
        readonly TaskFactory _factory;
        readonly SimRuntime _runtime;

        public SimService(SimRuntime runtime, ServiceId id, Func<SimProc, Task> launcher) {
            _id = id;
            _launcher = launcher;
            _runtime = runtime;
            
            _scheduler = new SimScheduler(runtime, id);
            _factory = new TaskFactory(_scheduler);
        }

        public void Launch(Action<Task> done) {
            if (_task != null && !_task.IsCompleted) {
                throw new InvalidOperationException($"Can't launch {_id} while previous instance is {_task.Status}");
            }

            var procID = _runtime.NextProcID();
            var env = new SimProc(_id, _runtime, procID, _factory);
            
            _task = _factory.StartNew(() => _launcher(env).ContinueWith(done)).Unwrap();
            _proc = env;

        }

        public async Task Stop(TimeSpan grace) {
            if (_task == null || _task.IsCompleted) {
                return;
            }

            _proc.Cancel();

            var finished = await Task.WhenAny(_task, _proc.Delay(grace, CancellationToken.None));
            if (finished != _task) {
                _proc.Debug("Shutdown timeout. ERASING FUTURE to KILL");
                _runtime.FutureQueue.Erase(_scheduler);
                _proc.Kill();
            }

            _proc = null;
            _task = null;
        }


     
        
    }
}