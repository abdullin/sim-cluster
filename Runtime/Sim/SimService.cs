using System;
using System.ComponentModel.DataAnnotations;
using System.Threading;
using System.Threading.Tasks;
using SimMach.Sim;

namespace SimMach.Sim {
    class SimService {
        public readonly ServiceId Id;
        readonly Func<IEnv, IEngine> _launcher;
        
        Task _task;
        SimProc _proc;
        
        
        readonly SimScheduler _scheduler;
        readonly TaskFactory _factory;
        readonly SimMachine _machine;

        public SimService(SimMachine machine, ServiceId id, Func<IEnv, IEngine> launcher) {
            Id = id;
            _launcher = launcher;
            _machine = machine;
            
            _scheduler = new SimScheduler(machine.Runtime, id);
            _factory = new TaskFactory(_scheduler);
        }

        public void Launch(Action<Exception> done) {
            if (_task != null && !_task.IsCompleted) {
                throw new InvalidOperationException($"Can't launch {Id} while previous instance is {_task.Status}");
            }

            var procID = _machine.NextProcID();
            var env = new SimProc(Id, _machine, procID, _factory);
            
            _task = _factory.StartNew(async () => {
                try {
                    var engine = _launcher(env);
                    try {
                        await engine.Run();
                    } finally {
                        await engine.Dispose();
                    }
                } catch (AggregateException ex) {
                    done(ex.InnerException);
                    return;
                }
                catch (Exception ex) {
                    done(ex);
                    return;
                }

                done(null);

            }).Unwrap();
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
                _machine.Runtime.FutureQueue.Erase(_scheduler);
            }
            
            ReleaseResources();
        }


        public void ReleaseResources() {
            if (_proc != null) {
                _proc.Dispose();
                _proc = null;
            }

            _task = null;

        }
    }
}