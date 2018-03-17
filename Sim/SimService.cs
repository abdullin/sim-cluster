using System;
using System.Threading;
using System.Threading.Tasks;
using SimMach.Sim;

namespace SimMach.Sim {
    
    
    class SimService {
        public readonly ServiceId Id;
        readonly Func<SimEnv, Task> _launcher;
        
        Task _task;
        SimEnv _env;

        public void Launch(Action<Task> done) {
            if (_task != null && !_task.IsCompleted) {
                throw new InvalidOperationException($"Can't launch {Id} while previous instance is {_task.Status}");
            }
            
            var env = new SimEnv(Id, _runtime);
            
            _task = _factory.StartNew(() => _launcher(env).ContinueWith(done)).Unwrap();
            _env = env;

        }

        public async Task Stop(int grace) {
            if (_task == null || _task.IsCompleted) {
                return;
            }

            _env.Cancel();

            var finished = await Task.WhenAny(_task, _env.Delay(grace, CancellationToken.None));
            if (finished != _task) {
                _env.Debug("Shutdown timeout. ERASING FUTURE to KILL");
                _runtime.FutureQueue.Erase(_scheduler);
                _env.Kill();
            }

            _env = null;
            _task = null;
        }


        readonly SimScheduler _scheduler;
        readonly TaskFactory _factory;
        readonly SimRuntime _runtime;

        public SimService(SimRuntime runtime, ServiceId id, Func<SimEnv, Task> launcher) {
            Id = id;
            _launcher = launcher;
            _runtime = runtime;
            
            _scheduler = new SimScheduler(runtime, id);
            _factory = new TaskFactory(_scheduler);
        }
        
    }
}