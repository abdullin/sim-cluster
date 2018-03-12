﻿using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Threading;
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
            
            // need to get to the inner task
            
            // TODO: a task should return an exit code
            _task = _factory.StartNew(() => _launcher(env).ContinueWith(done)).Unwrap();
            _sim = env;

        }

        public async Task Stop(int grace) {
            if (_task == null || _task.IsCompleted) {
                return;
            }

            _sim.Cancel();

            var finished = await Task.WhenAny(_task, Future.Delay(grace));
            if (finished != _task) {
                _sim.Debug("Shutdown timeout. ERASING FUTURE to KILL");
                _runtime.FutureQueue.Erase(_scheduler);
                _sim.Kill();
            }

            _sim = null;
            _task = null;
            // eliminate the future
            


        }


        readonly Scheduler _scheduler;
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
                TryExecuteTask(task);
            } 
            catch (Exception ex) {
                Console.WriteLine($"Failed executing {task} on {_name} {ex.Demystify()}");

            }

        }

        protected override IEnumerable<Task> GetScheduledTasks() {
            throw new NotImplementedException();
        }

        protected override void QueueTask(Task task) {
            
            switch (task) {
                case Future ft:
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

    sealed class Future : Task {
        public readonly TimeSpan Ts;
        public readonly CancellationToken Token;
        

        public Future(TimeSpan ts, CancellationToken token) : base(() => {
            if (token.IsCancellationRequested) {
                throw new TaskCanceledException();
            }
            
        }) {
            // future cancellation doesn't propagate
            Ts = ts;
            Token = token;
        }
        
        // TODO: should react to the service cancellation token
        public static Task Delay(TimeSpan ts, CancellationToken token = default (CancellationToken)) {
            var task = new Future(ts, token);
            task.Start();
            return task;
        }
        
        public static Task Delay(int ms, CancellationToken token = default (CancellationToken)) {
            return Delay(TimeSpan.FromMilliseconds(ms), token);
        }
    }
}