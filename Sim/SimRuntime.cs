using System;
using System.Collections.Generic;
using System.ComponentModel.Design;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Threading.Tasks;

namespace SimMach {
    class SimRuntime {
        public readonly Dictionary<ServiceId, SimService> Services;
        
        
        
        public TimeSpan Time => TimeSpan.FromTicks(_time);
        public long Ticks => _time;
        
        public TimeSpan Timeout {
            set { MaxTime = value.Ticks; }
        }

        long _steps;
        long _time;

        public long MaxTime = long.MaxValue;
        
        
        
        public readonly SimFutureQueue FutureQueue = new SimFutureQueue();

        // system schedulers
        readonly SimScheduler _scheduler;
        readonly TaskFactory _factory;

        public SimRuntime(Topology topology) {
            Services = topology.ToDictionary(p => p.Key, p => new SimService(this, p.Key, p.Value));
            
            _scheduler = new SimScheduler(this,new ServiceId("simulation:proc"));
            _factory = new TaskFactory(_scheduler);
        }
        
        
        public void Schedule(SimScheduler id, TimeSpan offset, object message) {
            _steps++;
            var pos = _time + offset.Ticks;
            FutureQueue.Schedule(id, pos, message);
        }

        public void Plan(Func<Task> a) {
            Schedule(_scheduler,TimeSpan.Zero, _factory.StartNew(a));
        }


        IEnumerable<SimService> Filter(Predicate<ServiceId> filter) {
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

        Exception _halt;

        public void Run() {
            _halt = null;
            
            var watch = Stopwatch.StartNew();
            var reason = "none";
            Debug($"{"start".ToUpper()}");
            
            try {
                var step = 0;
                while (true) {
                    step++;
                 

                    var hasFuture = FutureQueue.TryGetFuture(out var o);
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

                    if (_halt != null) {
                        reason = "halt";
                        break;
                    }

                    if ((_time - _lastActivity) >= _terminateIfNoActivityFor) {
                        reason = "no activity";
                        break;
                    }

                    if (_time >= MaxTime) {
                        reason = "end";
                        break;
                    }
                }
            } catch (Exception ex) {
                reason = "fatal";
                _halt = ex;
                Console.WriteLine("Fatal: " + ex);
            }
            
            watch.Stop();

            var softTime = TimeSpan.FromTicks(_time);
            var factor = softTime.TotalHours / watch.Elapsed.TotalHours;
            Debug($"{reason.ToUpper()}");
            //Console.WriteLine("Simulation parameters:");
            

            if (_halt != null) {
                Console.WriteLine(_halt.Demystify());
            }

            Console.WriteLine($"Simulated {Moment.Print(softTime)} in {_steps} steps.");
            Console.WriteLine($"Took {Moment.Print(watch.Elapsed)} of real time (x{factor:F0} speed-up)");

        }
  
        public void StartServices(Predicate<ServiceId> selector = null) {
            foreach (var svc in Filter(selector)) {
                svc.Launch(task => {
                    if (task.Exception != null) {
                        _halt = task.Exception.InnerException;
                    }
                });
            }
        }
        
        public Task StopServices(Predicate<ServiceId> selector = null, int grace = 1000) {
            var tasks = Filter(selector).Select(p => p.Stop(grace)).ToArray();
            return Task.WhenAll(tasks);
        }
    }
}