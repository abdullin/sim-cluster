using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Threading.Tasks;

namespace SimMach {
    sealed class SimScheduler : TaskScheduler {
        readonly SimRuntime _sim;
        readonly ServiceId _name;

        public SimScheduler(SimRuntime sim, ServiceId name) {
            _sim = sim;
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
                _sim.Debug($"Failed executing {task} on {_name} {ex.Demystify()}");
            }

        }

        protected override IEnumerable<Task> GetScheduledTasks() {
            throw new NotImplementedException();
        }

        protected override void QueueTask(Task task) {
            
            switch (task) {
                case SimFutureTask ft:
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
}