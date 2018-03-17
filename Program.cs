using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Numerics;
using System.Reflection.Metadata.Ecma335;
using System.Threading;
using System.Threading.Tasks;
using SimMach.Sim;

namespace SimMach
{

   
    class Program {
        static void Main(string[] args) {

            var bootCount = 0;

            var test = new SimTest() {
                MaxTime = TimeSpan.FromMinutes(2)
            };

            test.AddService("com:test", async env => {
                bootCount++;
                while (!env.Token.IsCancellationRequested) {
                    await env.SimulateWork(100);
                }
            });
            
            test.RunPlan(async plan => {
                plan.StartServices();
                await plan.Delay(TimeSpan.FromMinutes(1));
                await plan.StopServices();
                plan.StartServices();
            });

            /*var t = new Topology {
                {"lb0.eu-west:slow", SlowProcess},
                {"lb0.eu-west:quick", QuickProcess},
                {"lb1.eu-west:slow", SlowProcess},
                {"lb1.eu-west:quick", QuickProcess}
            };
            var sim = new SimRuntime(t) {
                Timeout = TimeSpan.FromSeconds(10)
            };
            
            sim.Plan(async plan => {
                plan.StartServices();
                await plan.Delay(1000);
                
                plan.Debug($"Power off 'lb1.eu-west' in 2s");
                await plan.StopServices(s => s.Machine == "lb1.eu-west", 2000);
                
                plan.Debug($"'lb1.eu-west' is down. Booting in 3s");
                await plan.Delay(3000);
                
                plan.StartServices(s => s.Machine == "lb1.eu-west");
                plan.Debug($"'lb1.eu-west' is up and running");
            });
            
            sim.Run();*/
        }

        static async Task QuickProcess(IEnv env) {
            env.Debug("Starting");
            try {
                while (!env.Token.IsCancellationRequested) {
                    await env.Delay(1000, env.Token);
                }
            } catch (TaskCanceledException) {
                env.Debug("Abort");
            }
            env.Debug("Shutting down");
        }
        static async Task SlowProcess(IEnv env) {
            env.Debug("Starting");
            try {
                while (!env.Token.IsCancellationRequested) {
                    await env.Delay(5000, env.Token);
                }
            } catch (TaskCanceledException) {
                env.Debug("Abort");
            }
            env.Debug("Shutting down");
        }
    }
}
