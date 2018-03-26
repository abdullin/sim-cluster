using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Threading.Tasks;

namespace SimMach.Sim {
    sealed class SimScheduler : TaskScheduler {
        readonly SimRuntime _runtime;
        readonly ServiceId _name;

        public SimScheduler(SimRuntime runtime, ServiceId name) {
            _runtime = runtime;
            _name = name;
        }

        public void Execute(Task task) {
            if (task.Status == TaskStatus.RanToCompletion) {
                return;
            }


            try {
                if (!TryExecuteTask(task)) {
                    //_sim.Debug($"Didn't execute a task {task.GetType().Name} ({task.Status})");
                }
            } 
            catch (Exception ex) {
                _runtime.Debug($"Failed executing {task} on {_name} {ex.Demystify()}");
            }

        }

        protected override IEnumerable<Task> GetScheduledTasks() {
            throw new NotImplementedException();
        }

        protected override void QueueTask(Task task) {
            
            switch (task) {
                case IFutureJump ft:
                    _runtime.Schedule(this, ft.Deadline, ft);
                    break;
                
                default:
                    _runtime.Schedule(this, TimeSpan.Zero, task);
                    break;
            }
        }

        protected override bool TryExecuteTaskInline(Task task, bool taskWasPreviouslyQueued) {
            return TryExecuteTask(task);
        }
    }
}