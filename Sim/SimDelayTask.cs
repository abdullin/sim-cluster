using System;
using System.Threading;
using System.Threading.Tasks;

namespace SimMach.Sim {
    sealed class SimDelayTask : Task, IFutureJump {
        readonly CancellationToken _token;

        SimDelayTask(TimeSpan ts, CancellationToken token) : base(() => {
            if (token.IsCancellationRequested) {
                throw new TaskCanceledException();
            }
        }) {
            // We don't pass the token to the base task. Just like with Task.Delay
            // we want the task to blow up on awaiter, instead of simply
            // stopping further execution.
            Deadline = ts;
            _token = token;
        }

        public new static Task Delay(TimeSpan ts, CancellationToken token = default(CancellationToken)) {
            var task = new SimDelayTask(ts, token);
            task.Start();
            return task;
        }

        public new static Task Delay(int ms, CancellationToken token = default(CancellationToken)) {
            return Delay(TimeSpan.FromMilliseconds(ms), token);
        }

        public bool FutureIsNow => _token.IsCancellationRequested;
        public TimeSpan Deadline { get; }
    }

   
}