using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Reflection.Metadata.Ecma335;
using System.Threading;
using System.Threading.Tasks;

namespace SimMach
{
    class Program {

        const string LoadBalancer = "lb.us-west";
        


        static void Main(string[] args) {



            var t = new Topology {
                {"lb0.eu-west:nginx", RunNginx},
                {"lb0.eu-west:telegraf", RunTelegraf},
                {"lb1.eu-west:nginx", RunNginx},
                {"lb1.eu-west:telegraf", RunTelegraf}
            };
            var runtime = new Runtime(t);
            runtime.Timeout = TimeSpan.FromSeconds(10);
            

            var machines = runtime.Services.GroupBy(p => p.Key.Machine);
            // printing
            foreach (var m in machines) {
                Console.WriteLine($"{m.Key}");

                foreach (var svc in m) {
                    Console.WriteLine($"  {svc.Key.Service}");
                }
            }
            
            
            
            runtime.Plan(async () => {
                runtime.Start();
                await Future.Delay(2000);
                
                
                const string machine = "lb1.eu-west";
                
                runtime.Debug($"Shutting '{machine}' down");
                await runtime.ShutDown(s => s.Machine == machine, 2000);
                
                runtime.Debug($"{machine} is down. Booting");
                await Future.Delay(2000);
                
                runtime.Start(s => s.Machine == machine);
                runtime.Debug($"{machine} is up and running");

            });
            
            // blocking call
            runtime.Run();

            Console.WriteLine("Done");

            // at the time of the configuration, simulation doesn't exist, yet!
        }

        static async Task RunTelegraf(Sim env) {
            
            env.Debug("Starting");
            try {
                while (!env.Token.IsCancellationRequested) {
                    //env.Debug("Running");
                    await Future.Delay(1000);
                }
            } catch (TaskCanceledException) { }

            env.Debug("Shutting down");
        }

        static async Task RunNginx(Sim env) {
            env.Debug("Starting");
            try {
                while (!env.Token.IsCancellationRequested) {
                    //env.Debug("Running");
                    await Future.Delay(5000);
                }
            } catch (TaskCanceledException) { }

            env.Debug("Shutting down");
        }
    }

    class Topology : Dictionary<ServiceId, Func<Sim, Task>> {
        public Topology() : base(new ServiceNameComparer()) { }
    }


    


    
    
}
