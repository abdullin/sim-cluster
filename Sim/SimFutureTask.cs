using System;
using System.Threading;
using System.Threading.Tasks;

namespace SimMach {
    sealed class SimFutureTask : Task {
        public readonly TimeSpan Ts;
        public readonly CancellationToken Token;

        SimFutureTask(TimeSpan ts, CancellationToken token) : base(() => {
            if (token.IsCancellationRequested) {
                throw new TaskCanceledException();
            }
        }) {
            // We don't pass the token to the base task. Just like with Task.Delay
            // we want the task to blow up on awaiter, instead of simply
            // stopping further execution.
            Ts = ts;
            Token = token;
        }
        
        static Task Delay(TimeSpan ts, CancellationToken token = default (CancellationToken)) {
            var task = new SimFutureTask(ts, token);
            task.Start();
            return task;
        }
        
        public static Task Delay(int ms, CancellationToken token = default (CancellationToken)) {
            return Delay(TimeSpan.FromMilliseconds(ms), token);
        }
    }
}