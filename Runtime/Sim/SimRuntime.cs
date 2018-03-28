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

namespace SimMach.Sim {
    public class SimRuntime  {
       
        
        public readonly ClusterDef Def;
        
        
        public TimeSpan Time => TimeSpan.FromTicks(_time);

        public long Ticks => _time;
        
        public TimeSpan MaxTime {
            set { MaxTicks = value.Ticks; }
        }

        public TimeSpan MaxInactive {
            set { _maxInactiveTicks = value.Ticks; }
        }

        long _steps;
        long _time;

        public long MaxTicks = long.MaxValue;
        public long MaxSteps = long.MaxValue;
        
        long _maxInactiveTicks = TimeSpan.FromSeconds(60).Ticks;

        int _traces;


        public readonly SimFutureQueue FutureQueue = new SimFutureQueue();

        // system schedulers
        readonly SimScheduler _scheduler;
        readonly TaskFactory _factory;

        string _folder;

        public string GetSimulationFolder() {
            if (_folder != null) {
                return _folder;
            }

            var folder = Path.Combine("/Volumes/SimDisk", "sim", DateTime.UtcNow.ToString("yy-MM-dd-HH-mm-ss"));
            _folder = folder;
            return folder;
        }

        public SimRuntime(ClusterDef def) {

            Def = def;
            _scheduler = new SimScheduler(this,new ServiceId("simulation:proc"));
            _factory = new TaskFactory(_scheduler);
        }

        
        public void Schedule(SimScheduler id, TimeSpan offset, object message) {
            _steps++;

            
            if (offset == Timeout.InfiniteTimeSpan) {
                FutureQueue.Schedule(id, SimFutureQueue.Never, message);
            } else {
                var pos = _time + offset.Ticks;
                FutureQueue.Schedule(id, pos, message);
            }

        }

        
      
        
        long _lastDebug;

        
        
        long _lastActivity;
        

        public void RecordActivity() {
            _lastActivity = _time;
        }

        public SimRandom Rand = new SimRandom(0);


        

        public void Debug(string message) {
            _lastActivity = _time;
            
            if (true) {
                var diff = _time - _lastDebug;
                _lastDebug = _time;
                Console.WriteLine("+ {0:0000} ms {1}", TimeSpan.FromTicks(diff).TotalMilliseconds, message);
            }
        }

        Exception _haltError;
        string _haltMessage;

        public void Halt(string message, Exception error) {
            _haltError = error;
            _haltMessage = message;
        }
        
        

        public void Run(Func<SimControl, Task> plan) {
            _haltError = null;

            var watch = Stopwatch.StartNew();
            var reason = "none";
            
            Debug($"{"start".ToUpper()}");
            Rand.Reinitialize(0);

            using (var cluster = new SimCluster(Def, this)) {
                
                Schedule(_scheduler,TimeSpan.Zero, _factory.StartNew(async () => {
                    var control = new SimControl(cluster, this);
                    try {
                        await plan(control);
                    } catch (Exception ex) {
                        Halt("Plan failed", ex);
                    }
                }));


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

                        if (_haltError != null || _haltMessage != null) {
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
                    _haltError = ex;
                    Console.WriteLine("Fatal: " + ex);
                } finally {
                    if (_folder != null) {
                        Directory.Delete(_folder, true);
                    }
                }

                watch.Stop();

                var softTime = TimeSpan.FromTicks(_time);
                var factor = softTime.TotalHours / watch.Elapsed.TotalHours;

                if (_haltMessage != null) {
                    reason = _haltMessage.ToUpper();
                }

                Debug($"{reason.ToUpper()} at {softTime}");

                if (_haltError != null) {
                    var demystify = _haltError.Demystify();
                    Console.WriteLine(demystify.Message);
                    Console.WriteLine(demystify.StackTrace);
                }

                Console.WriteLine($"Simulated {Moment.Print(softTime)} in {_steps} steps.");
                Console.WriteLine($"Took {Moment.Print(watch.Elapsed)} of real time (x{factor:F0} speed-up)");

            }

            

        }

        
    }
}