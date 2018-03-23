using NUnit.Framework;
using SimMach.Playground.Backend;
using SimMach.Playground.CommitLog;
using SimMach.Sim;

namespace SimMach.Playground {
    public sealed class PlaygroundTests {


        [Test]
        public void Playground() {
            var sim = new TestRuntime() {
                //DebugNetwork = true,
                MaxTime = 50.Sec()
            };
            
            sim.Net.Link("client", "api1", "api2");
            sim.Net.Link("mv", "api1", "api2");
            
            sim.Net.TraceRoute("client", "api1");
            
            sim.Svc.Add("mv", env => new CommitLogServer(env, 443).Run());

            sim.Svc.Add(new[] {"api1", "api2"}, env => {
                var client = new CommitLogClient(env, "mv:443");
                return new BackendServer(env, 443, client).Run();
            });

            decimal finalCount = 0M;
            
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
            });
            
            sim.RunPlan(async plan => {
                plan.StartServices();
                await plan.Delay(6.Sec());
                plan.Debug("STOP api1");
                await plan.StopServices(s => s.Machine == "api1");
                await plan.Delay(2.Sec());
                plan.Debug("START api1");
                plan.StartServices(s => s.Machine == "api1");
            });
            
            Assert.AreEqual(1M, finalCount, nameof(finalCount));
        }
    }
}