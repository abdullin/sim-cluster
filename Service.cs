using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Threading.Tasks;

namespace SimMach {
    
    
    class Service {
        
        public readonly ServiceId Id;
        readonly Func<Sim, Task> _launcher;
        
        



        Task _task;
        public Sim _sim;

        public void Launch(Action<Task> done) {
            if (_task != null && !_task.IsCompleted) {
                throw new InvalidOperationException($"Can't launch {Id} while previous instance is {_task.Status}");
            }
            
            var env = new Sim(Id, _runtime);
            
            _task = _factory.StartNew(() => _launcher(env).ContinueWith(done));
            _sim = env;

        }

        /* public async Task Stop(int grace) {
            if (_task == null || _task.IsCompleted) {
                return;
            }
            
            _sim.Cancel();
            

            var finished = await Task.WhenAny(_task, Task.Delay(grace));
            if (finished != _task) {
                _sim.Debug("Killing the process");
                _sim.Kill();
            }
            _sim = null;
            _task = null;
        } */


        readonly TaskScheduler _scheduler;
        readonly TaskFactory _factory;
        readonly Runtime _runtime;

        public Service(Runtime runtime, ServiceId id, Func<Sim, Task> launcher) {
            Id = id;
            _launcher = launcher;
            _runtime = runtime;
            
            _scheduler = new Scheduler(runtime, id);
            _factory = new TaskFactory(_scheduler);
        }
        
    }


    sealed class Scheduler : TaskScheduler {
        readonly Runtime _sim;
        readonly ServiceId _name;

        public Scheduler(Runtime sim, ServiceId name) {
            _sim = sim;
            _name = name;
        }

        public void Execute(Task task) {
            if (task.Status == TaskStatus.RanToCompletion) {
                return;
            }

            try {
                if (!TryExecuteTask(task)) {
                    throw new InvalidOperationException($"Can't execute {task}-{task.Status}");
                }
            } catch (Exception ex) {
                Console.WriteLine($"Failed executing {task}: {ex.Demystify()}");
            }
        }

        protected override IEnumerable<Task> GetScheduledTasks() {
            throw new NotImplementedException();
        }

        protected override void QueueTask(Task task) {
            switch (task) {
                case FutureTask ft:
                    _sim.Schedule(this, ft.Ts, ft);
                    break;
                default:
                    _sim.Schedule(this, TimeSpan.Zero, task);
                    break;
            }
        }

        protected override bool TryExecuteTaskInline(Task task, bool taskWasPreviouslyQueued) {
            return TryExecuteTask(task);
        }
    }

    sealed class FutureTask : Task {
        public readonly TimeSpan Ts;

        public FutureTask(TimeSpan ts) : base(() => { }) {
            Ts = ts;
        }
    }
}