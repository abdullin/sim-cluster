using NUnit.Framework;
using SimMach.Playground.Backend;
using SimMach.Playground.CommitLog;
using SimMach.Sim;

namespace SimMach.Playground {
    public sealed class PlaygroundTests {


        [Test]
        public void Playground() {
            var test = new TestDef() {MaxTime = 50.Sec()};
            
            test.LinkNets("bot", "public", latency:100.Ms());
            test.LinkNets("public", "internal", latency:15.Ms());
            
            InstallCommitLog(test, "cl.internal");
            InstallBackend(test, "api1.public", "api2.public");
            
            var finalCount = 0M;
            // run a bot that moves items between locations
            test.AddScript("bot", async env => {
                var lib = new BackendClient(env, "api1.public:443", "api2.public:443");
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
            
            test.RunPlan(async plan => {
                plan.StartServices();
                await plan.Delay(6.Sec());
                plan.Debug("REIMAGE api1");
                await plan.StopServices(s => s.Machine == "api1.public", grace:1.Sec());
                plan.WipeStorage("api1");
                await plan.Delay(2.Sec());
                plan.Debug("START api1");
                plan.StartServices(s => s.Machine == "api1.public");
            });
            Assert.AreEqual(1M, finalCount, nameof(finalCount));
        }

        static void InstallBackend(ClusterDef sim, params string[] servers) {
            foreach (var server in servers) {
                sim.AddService(server, env => {
                    var client = new CommitLogClient(env, "cl.internal:443");
                    return new BackendServer(env, 443, client);
                });    
            }
        }

        static void InstallCommitLog(TestDef sim, string service) {
            sim.AddService(service, env => new CommitLogServer(env, 443));
        }
    }
    
    
}