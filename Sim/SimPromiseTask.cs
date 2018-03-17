using System;
using System.Threading;
using System.Threading.Tasks;

namespace SimMach.Sim {
    
    sealed class SimFuture<T> : IFuture<T> {
        internal T Result;
        internal Exception Error;
        public bool Completed;

        
        public Task<T> Task { get; }

        public void SetResult(T result) {
            
            Result = result;
            Completed = true;
        }

        public void SetError(Exception err) {
            Error = err;
            Completed = true;
        }

        
        public SimFuture(int timeoutMs, CancellationToken token = default (CancellationToken)) {
            Task = new SimFutureTask<T>(TimeSpan.FromMilliseconds(timeoutMs), token, this);
            Task.Start();
        }

        public SimFuture(TimeSpan timeout, CancellationToken token = default (CancellationToken)) {
            Task = new SimFutureTask<T>(timeout, token, this);
            Task.Start();
        }
    }
    
    sealed class SimFutureTask<T> : Task<T>, IFutureJump {


        readonly CancellationToken _token;
        readonly SimFuture<T> _source;


        public SimFutureTask(TimeSpan ts, 
            CancellationToken token, 
            SimFuture<T> source) : base (() => {
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