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
        static void Main(string[] args) {



            var t = new Topology {
                {"lb0.eu-west:slow", SlowProcess},
                {"lb0.eu-west:quick", QuickProcess},
                {"lb1.eu-west:slow", SlowProcess},
                {"lb1.eu-west:quick", QuickProcess}
            };
            var sim = new Runtime(t) {
                Timeout = TimeSpan.FromSeconds(10)
            };


            var machines = sim.Services.GroupBy(p => p.Key.Machine);
            // printing
            foreach (var m in machines) {
                Console.WriteLine($"{m.Key}");

                foreach (var svc in m) {
                    Console.WriteLine($"  {svc.Key.Service}");
                }
            }
            
            
            
            sim.Plan(async () => {
                sim.StartServices();
                await Future.Delay(1000);
                
                sim.Debug($"Power off 'lb1.eu-west' in 2s");
                await sim.StopServices(s => s.Machine == "lb1.eu-west", 2000);
                
                sim.Debug($"'lb1.eu-west' is down. Booting in 3s");
                await Future.Delay(3000);
                
                sim.StartServices(s => s.Machine == "lb1.eu-west");
                sim.Debug($"'lb1.eu-west' is up and running");
            });
            
            sim.Run();
        }

        static async Task QuickProcess(Env env) {
            env.Debug("Starting");
            try {
                while (!env.Token.IsCancellationRequested) {
                    await Future.Delay(1000, env.Token);
                }
            } catch (TaskCanceledException) {
                env.Debug("Abort");
            }
            env.Debug("Shutting down");
        }
        static async Task SlowProcess(Env env) {
            env.Debug("Starting");
            try {
                while (!env.Token.IsCancellationRequested) {
                    await Future.Delay(5000, env.Token);
                }
            } catch (TaskCanceledException) {
                env.Debug("Abort");
            }
            env.Debug("Shutting down");
        }
    }

    class Topology : Dictionary<ServiceId, Func<Env, Task>> {
        public Topology() : base(new ServiceIdComparer()) { }
    }


    


    
    
}
