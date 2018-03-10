using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace SimMach {
    class Runtime {
        public readonly Dictionary<ServiceName, Service> Services;
        
        
        
        
        readonly Stopwatch _watch = Stopwatch.StartNew();
        public TimeSpan Time => _watch.Elapsed;
        public long Ticks => _watch.ElapsedTicks;

        public Runtime(Topology topology) {
            


            Services = topology.ToDictionary(p => p.Key, p => new Service(p.Key, p.Value));
        }


        IEnumerable<Service> Filter(Predicate<ServiceName> filter) {
            if (null == filter) {
                return Services.Values;
            }

            return Services.Where(p => filter(p.Key)).Select(p => p.Value);
        }

        public void Start(Predicate<ServiceName> selector = null) {
            foreach (var svc in Filter(selector)) {
                svc.Launch();
            }
        }

        public Task ShutDown(Predicate<ServiceName> selector = null, int grace = 1000) {
            var tasks = Filter(selector).Select(p => p.Stop(grace)).ToArray();
            return Task.WhenAll(tasks);
        }
    }
    
    
    class Service {
        
        public readonly ServiceName Name;
        readonly Func<Sim, Task> _launcher;



        Task _task;
        public Sim _sim;

        public void Launch() {
            if (_task != null && !_task.IsCompleted) {
                throw new InvalidOperationException($"Can't launch {Name} while previous instance is {_task.Status}");
            }
            
            
            var env = new Sim(this);
            _task = Task.Factory.StartNew(() => _launcher(env).Wait());
            _sim = env;

        }

        public async Task Stop(int grace) {
            if (_task == null || _task.IsCompleted) {
                return;
            }
            
            _sim.Cancel();
            

            var finished = await Task.WhenAny(_task, Task.Delay(grace));
            if (finished != _task) {
                _sim.Debug("Killing the process");
                _sim.Kill();
            }
            _sim = null;
            _task = null;
        }


        public Service(ServiceName name, Func<Sim, Task> launcher) {
            
            Name = name;
            _launcher = launcher;
        }
        
    }

  




    class Sim {
        readonly Service Service;
        readonly Runtime Runtime;

        readonly CancellationTokenSource _cts;

        public CancellationToken Token => _cts.Token;


        readonly long[] _failures; 

        public Sim(Service service) {
            _cts = new CancellationTokenSource();
            Service = service;
            
            _failures = new long[Enum.GetValues(typeof(Effect)).Length];
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

        public async Task SpinDisk() {
            if (Has(Effect.DiskLagging)) {
                Debug("Disk is down");
                await Task.Delay(TimeSpan.FromSeconds(10));
            }
        }
        

        public void Debug(string message) {
            if (_killed) {
                Console.WriteLine($"ZOMBIE!!! {Service.Name}: {message}");
            } else {
                Console.WriteLine($"{Service.Name}: {message}");
            }
        }
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