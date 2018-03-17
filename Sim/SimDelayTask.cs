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

    sealed class SimCompletionSource<T>  {
        internal T Result;
        internal Exception Error;
        public bool Completed;

        
        public readonly SimPromise<T> Task;

        public void SetResult(T result) {
            
            Result = result;
            Completed = true;
        }

        public void SetError(Exception err) {
            Error = err;
            Completed = true;
        }

        

        public SimCompletionSource(TimeSpan timeout, CancellationToken token = default (CancellationToken)) {
            
            Task = new SimPromise<T>(timeout, token, this);
            Task.Start();
        }
    }

    sealed class SimPromise<T> : Task<T>, IFutureJump {


        readonly CancellationToken _token;
        readonly SimCompletionSource<T> _source;


        public SimPromise(TimeSpan ts, 
            CancellationToken token, 
            SimCompletionSource<T> source) : base (() => {
            if (token.IsCancellationRequested) {
                throw new TaskCanceledException();
            }

            if (!source.Completed) {
                throw new TimeoutException();
            }

            if (source.Error != null) {
                throw source.Error;
            }

            return source.Result;

        }) {
            Deadline = ts;
            _token = token;
            _source = source;
        }


        public bool FutureIsNow => _token.IsCancellationRequested || _source.Completed;
        public TimeSpan Deadline { get; }
    }
}