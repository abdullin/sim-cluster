using NUnit.Framework;

namespace SimMach.Sim {
    public sealed class EdaSetups {


        [Test]
        public void Playground() {
            var sim = new TestRuntime() {
                //DebugNetwork = true,
                MaxTime = 50.Sec()
            };
            
            sim.Net.Link("client", "api1", "api2");
            sim.Net.Link("mv", "api1", "api2");
            
            sim.Net.TraceRoute("client", "api1");
            
            sim.Svc.Add("mv", env => new CommitLog(env, 443).Run());

            sim.Svc.Add(new[] {"api1", "api2"}, env => {
                var client = new CommitLogClient(env, "mv:443");
                return new Backend(env, 443, client).Run();
            });

            decimal finalCount = 0M;
            
            sim.Svc.Add("client", async env => {
                var lib = new ClientLib(env, "api1:443", "api2:443");
                const int ringSize = 5;
                await lib.AddItem(0, 1);
                for (int i = 0; i < 10; i++) {
                    var curr = i % ringSize;
                    var next = (i + 1) % ringSize;
                    await lib.MoveItem(curr, next, 1);
                }

                finalCount = await lib.Count();
            });
            
            sim.RunPlan(async plan => {
                plan.StartServices();
                await plan.Delay(18.Sec());
                plan.Debug("STOP api1");
                await plan.StopServices(s => s.Machine == "api1");
                await plan.Delay(10.Sec());
                plan.Debug("START api1");
                plan.StartServices(s => s.Machine == "api1");
            });
            
            Assert.AreEqual(1M, finalCount, nameof(finalCount));
        }
    }
}