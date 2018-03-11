using System;
using System.Collections.Generic;
using System.ComponentModel.Design;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Threading;
using System.Threading.Tasks;

namespace SimMach {
    class Runtime {
        public readonly Dictionary<ServiceId, Service> Services;
        
        
        
        public TimeSpan Time => TimeSpan.FromTicks(_time);
        public long Ticks => _time;
        
        public TimeSpan Timeout {
            set { MaxTime = value.Ticks; }
        }

        long _steps;
        long _time;

        public long MaxTime = long.MaxValue;
        
        
        
        public readonly Future Future = new Future();

        // system schedulers
        readonly Scheduler _scheduler;
        readonly TaskFactory _factory;

        public Runtime(Topology topology) {
            Services = topology.ToDictionary(p => p.Key, p => new Service(this, p.Key, p.Value));
            
            _scheduler = new Scheduler(this,new ServiceId("simulation:proc"));
            _factory = new TaskFactory(_scheduler);
        }
        
        
        public void Schedule(Scheduler id, TimeSpan offset, object message) {
            _steps++;
            var pos = _time + offset.Ticks;
            Future.Schedule(id, pos, message);
        }

        public void Plan(TimeSpan offset, Action a) {
            
            Schedule(_scheduler,TimeSpan.Zero, _factory.StartNew(() => {
                var futureTask = new FutureTask(offset);
                futureTask.Start();
                futureTask.ContinueWith(_ => a());
            }));
        }


        IEnumerable<Service> Filter(Predicate<ServiceId> filter) {
            if (null == filter) {
                return Services.Values;
            }

            return Services.Where(p => filter(p.Key)).Select(p => p.Value);
        }
        
        long _lastDebug;

        
        
        long _lastActivity;
        readonly long _terminateIfNoActivityFor = TimeSpan.FromSeconds(5).Ticks;

        
        public void Debug(string message) {
            _lastActivity = _time;
            
            if (true) {
                var diff = _time - _lastDebug;
                _lastDebug = _time;
                Console.WriteLine("+ {0:0000} ms {1}", TimeSpan.FromTicks(diff).TotalMilliseconds, message);
            }
        }

        public void Run() {
            var token = CancellationToken.None;

            Exception halt = null;
            foreach (var svc in Filter(id => true)) {
                svc.Launch(task => {
                    Console.WriteLine("Service died");
                    if (task.Exception != null) {
                        halt = task.Exception.InnerException;
                    }
                });
            }
            
            var watch = Stopwatch.StartNew();
            var reason = "none";
            
            
            try {
                var step = 0;
                while (true) {
                    step++;
                 

                    var hasFuture = Future.TryGetFuture(out var o);
                    if (!hasFuture) {
                        reason = "died";
                        break;
                    }

                    if (o.Time > _time) {
                        _time = o.Time;
                    }

                    switch (o.Item) {
                        /*case NetworkRequest nr:
                            Socket value;
                            if (network.TryGetValue(nr.Endpoint, out value)) {
                                network[nr.Endpoint].DispatchRequest(nr); 
                            } else {
                                Factory.StartNew(() => nr.Reply(new IOException("No route to host")));
                            }
                            break; */
                        case Task t:
                            o.Scheduler.Execute(t);
                            break;
                        default:
                            throw new InvalidOperationException();
                    }

                    if (halt != null) {
                        reason = "halt";
                        break;
                    }

                    if ((_time - _lastActivity) >= _terminateIfNoActivityFor) {
                        reason = "no activity";
                        break;
                    }

                    if (_time >= MaxTime) {
                        reason = "timeout";
                        break;
                    }
                }
            } catch (Exception ex) {
                reason = "fatal";
                halt = ex;
                Console.WriteLine("Fatal: " + ex);
            }
            
            watch.Stop();

            var softTime = TimeSpan.FromTicks(_time);
            var factor = softTime.TotalHours / watch.Elapsed.TotalHours;
            Debug($"Result: {reason.ToUpper()}");
            Console.WriteLine("Simulation parameters:");
            

            if (halt != null) {
                Console.WriteLine(halt.Demystify());
            }

            Console.WriteLine($"Simulated {softTime.TotalHours:F1} hours in {_steps} steps.");
            Console.WriteLine($"Took {watch.Elapsed.TotalSeconds:F1} seconds of real time (x{factor:F0} speed-up)");

        }

        /*
        public void Start(Predicate<ServiceId> selector = null) {
            foreach (var svc in Filter(selector)) {
                svc.Launch();
            }
        }*/

        public Task ShutDown(Predicate<ServiceId> selector = null, int grace = 1000) {
            Debug("Shutting down");
            var tasks = Filter(selector).Select(p => p.Stop(grace)).ToArray();
            return Task.WhenAll(tasks);
        }
    }


    class Sim {
        
        readonly ServiceId Id;
        readonly Runtime Runtime;
        

        readonly CancellationTokenSource _cts;

        public CancellationToken Token => _cts.Token;


        readonly long[] _failures; 

        public Sim(ServiceId id, Runtime runtime) {
            _cts = new CancellationTokenSource();
            Id = id;
            Runtime = runtime;
            
            
            _failures = new long[Enum.GetValues(typeof(Effect)).Length];
        }

        public Task Delay(TimeSpan ts) {
            var task = new FutureTask(ts);
            task.Start();
            return task;
        }

        public Task Delay(int ms) {
            return Delay(TimeSpan.FromMilliseconds(ms));
        }


        public void Plan(Effect effect, long till) {
            _failures[(byte) effect] = till;
        }

        public bool Has(Effect failure) {
            return _failures[(byte) failure] > Runtime.Ticks;
        }

        public void Cancel() {
            // issues a soft cancel token
            _cts.Cancel();
        }

        // killed sim denies all interactions with the outside world
        bool _killed;

        public void Kill() {
            _killed = true;
        }

        /*public async Task SpinDisk() {
            if (Has(Effect.DiskLagging)) {
                Debug("Disk is down");
                await Task.Delay(TimeSpan.FromSeconds(10));
            }
        }*/
        
        public void Debug(string l) {
            Runtime.Debug($" {Id.Machine,-11} {Id.Service,-20} {l}");
        }
        /*public void Debug(string message) {
            if (_killed) {
                Console.WriteLine($"ZOMBIE!!! {Service.Name}: {message}");
            } else {
                Console.WriteLine($"{Service.Name}: {message}");
            }
        }*/
    }


    public enum Effect : byte{
        None,
        DiskLagging
    }

    public struct Failure {
        public readonly Effect Effect;
        public readonly long Till;

        public Failure(Effect effect, long till) {
            Effect = effect;
            Till = till;
        }
    }
}