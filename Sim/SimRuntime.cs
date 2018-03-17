using System;
using System.Collections.Generic;
using System.ComponentModel.Design;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Threading.Tasks;

namespace SimMach.Sim {
    class SimRuntime : ISimPlan {
        public readonly Dictionary<ServiceId, SimService> Services;
        public readonly SimNetwork Network;
        
        
        public TimeSpan Time => TimeSpan.FromTicks(_time);
        public long Ticks => _time;
        
        public TimeSpan MaxTime {
            set { MaxTicks = value.Ticks; }
        }

        long _steps;
        long _time;

        public long MaxTicks = long.MaxValue;
        public long MaxSteps = long.MaxValue;
        
        long _maxInactiveTicks = TimeSpan.FromSeconds(60).Ticks;
        
        public readonly SimFutureQueue FutureQueue = new SimFutureQueue();

        // system schedulers
        readonly SimScheduler _scheduler;
        readonly TaskFactory _factory;

        public SimRuntime(MachineDef topology, NetworkDef net) {
            Services = topology.ToDictionary(p => p.Key, p => new SimService(this, p.Key, p.Value));
            Network = new SimNetwork(net, this);
            
            _scheduler = new SimScheduler(this,new ServiceId("simulation:proc"));
            _factory = new TaskFactory(_scheduler);
        }

        
        public void Schedule(SimScheduler id, TimeSpan offset, object message) {
            _steps++;
            var pos = _time + offset.Ticks;
            FutureQueue.Schedule(id, pos, message);
        }

        public void Plan(Func<ISimPlan, Task> a) {
            Schedule(_scheduler,TimeSpan.Zero, _factory.StartNew(() => a(this)));
        }


        IEnumerable<SimService> Filter(Predicate<ServiceId> filter) {
            if (null == filter) {
                return Services.Values;
            }

            return Services.Where(p => filter(p.Key)).Select(p => p.Value);
        }
        
        long _lastDebug;

        
        
        long _lastActivity;
        

        public void RecordActivity() {
            _lastActivity = _time;
        }


        public void Debug(string message) {
            _lastActivity = _time;
            
            if (true) {
                var diff = _time - _lastDebug;
                _lastDebug = _time;
                Console.WriteLine("+ {0:0000} ms {1}", TimeSpan.FromTicks(diff).TotalMilliseconds, message);
            }
        }

        Task ISimPlan.Delay(int i) {
            return SimDelayTask.Delay(i);
        }
        Task ISimPlan.Delay(TimeSpan i) {
            return SimDelayTask.Delay(i);
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

                    if ((_time - _lastActivity) >= _maxInactiveTicks) {
                        reason = "no activity " + Moment.Print(TimeSpan.FromTicks(_maxInactiveTicks));
                        break;
                    }

                    if (_steps >= MaxSteps) {
                        reason = MaxSteps + " steps reached";
                        break;
                    }

                    if (_time >= MaxTicks) {
                        reason = "max time";
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

        void ISimPlan.StartServices(Predicate<ServiceId> selector = null) {
            foreach (var svc in Filter(selector)) {
                svc.Launch(task => {
                    if (task.Exception != null) {
                        _halt = task.Exception.InnerException;
                    }
                });
            }
        }

        Task ISimPlan.StopServices(Predicate<ServiceId> selector = null, int grace = 1000) {
            var tasks = Filter(selector).Select(p => p.Stop(grace)).ToArray();
            return Task.WhenAll(tasks);
        }

        
        public Task<IConn> Listen(ServiceId server, int port) {

            var owner = Services[server];
            return Network.Listen(owner, port);
        }

        public Task<IConn> Connect(ServiceId client, SimEndpoint server) {
            var process = Services[client];
            return Network.Connect(process, server);
        }
    }
}