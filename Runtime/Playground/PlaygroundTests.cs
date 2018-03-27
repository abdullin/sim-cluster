using NUnit.Framework;
using SimMach.Playground.Backend;
using SimMach.Playground.CommitLog;
using SimMach.Sim;

namespace SimMach.Playground {
    public sealed class PlaygroundTests {


        [Test]
        public void Playground() {
            var sim = new TestRuntime() {MaxTime = 50.Sec()};
            // network connectivity
            sim.Net.Link("client", "api1", "api2");
            sim.Net.Link("mv", "api1", "api2");
            // install MV and backend
            InstallCommitLog(sim, "mv");
            InstallBackend(sim, "api1", "api2");
            
            var finalCount = 0M;
            // run a bot that moves items between locations
            sim.Svc.Add("client", async env => {
                var lib = new BackendClient(env, "api1:443", "api2:443");
                const int ringSize = 5;
                await lib.AddItem(0, 1);
                
                for (int i = 0; i < 5; i++) {
                    var curr = i % ringSize;
                    var next = (i + 1) % ringSize;
                    await lib.MoveItem(curr, next, 1);
                    await env.Delay(5.Sec());
                }
                finalCount = await lib.Count();
                env.Halt("DONE");
            });
            
            sim.RunPlan(async plan => {
                plan.StartServices();
                await plan.Delay(6.Sec());
                plan.Debug("REIMAGE api1");
               // await plan.StopServices(s => s.Machine == "api1", grace:1.Sec());
                plan.WipeStorage("api1");
                await plan.Delay(2.Sec());
                plan.Debug("START api1");
                //plan.StartServices(s => s.Machine == "api1");
            });
            Assert.AreEqual(1M, finalCount, nameof(finalCount));
        }

        static void InstallBackend(TestRuntime sim, params string[] servers) {
            foreach (var server in servers) {
                sim.Svc.Add(server, env => {
                    var client = new CommitLogClient(env, "mv:443");
                    return new BackendServer(env, 443, client);
                });    
            }
        }

        static void InstallCommitLog(TestRuntime sim, string service) {
            sim.Svc.Add(service, env => new CommitLogServer(env, 443));
        }
    }
}