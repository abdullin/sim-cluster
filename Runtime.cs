using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace SimMach {
    class Runtime {
        Topology _topology;

        Dictionary<string, Service> _services = new Dictionary<string, Service>();

        public Runtime(Topology topology) {
            _topology = topology;


            topology.ToDictionary(p => p.Key, p => new Service(p.Key, p.Value));
        }

        public void Start() {
            foreach (var (_, svc) in _services) {
                svc.Launch();
            }
        }

        public Task ShutDown(int grace = 1000) {
            var tasks = _services.Select(p => p.Value.Stop(grace)).ToArray();
            return Task.WhenAll(tasks);
        }
    }
    
    
    class Service {
        
        public readonly string Name;
        readonly Func<Sim, Task> _launcher;



        Task _task;
        Sim _sim;

        

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


        public Service(string name, Func<Sim, Task> launcher) {
            
            Name = name;
            _launcher = launcher;
        }
        
    }

  




    class Sim {
        readonly Service Service;

        readonly CancellationTokenSource _cts;

        public CancellationToken Token => _cts.Token;

        public Sim(Service service) {
            _cts = new CancellationTokenSource();
            Service = service;
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
        
        

        public void Debug(string message) {
            if (_killed) {
                Console.WriteLine($"ZOMBIE!!! {Service.Name}: {message}");
            } else {
                Console.WriteLine($"{Service.Name}: {message}");
            }
        }
    }
}