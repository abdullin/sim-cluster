using System;
using System.Collections.Generic;
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

            var machines = runtime.Services.GroupBy(p => p.Key.Machine);
            // printing
            foreach (var m in machines) {
                Console.WriteLine($"{m.Key}");

                foreach (var svc in m) {
                    Console.WriteLine($"  {svc.Key.Service}");
                }
            }
            
            //runtime.Services.First().Value.
            
            runtime.Start();


            Console.ReadLine();

            Console.WriteLine("Shutting down...");
            
            runtime.ShutDown(p => p.Machine == "lb0.eu-west").Wait();
            Console.WriteLine("Done. Starting");
            runtime.Start(p => p.Machine == "lb0.eu-west");
            Console.WriteLine("Booted");
            

            Console.ReadLine();

            // at the time of the configuration, simulation doesn't exist, yet!
        }

        static async Task RunTelegraf(Sim env) {
            env.Debug("Starting");
            try {
                while (!env.Token.IsCancellationRequested) {
                    env.Debug("Running");
                    await Task.Delay(1000);
                }
            } catch (TaskCanceledException) { }

            env.Debug("Shutting down");
        }

        static async Task RunNginx(Sim env) {
            env.Debug("Starting");
            try {
                while (!env.Token.IsCancellationRequested) {
                    env.Debug("Running");
                    await Task.Delay(5000, env.Token);
                }
            } catch (TaskCanceledException) { }

            env.Debug("Shutting down");
        }
    }

    class Topology : Dictionary<ServiceName, Func<Sim, Task>> {
        public Topology() : base(new ServiceNameComparer()) { }
    }


    


    
    
}
