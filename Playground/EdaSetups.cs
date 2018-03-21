using NUnit.Framework;

namespace SimMach.Sim {
    public sealed class EdaSetups {


        [Test]
        public void Playground() {
            var sim = new TestRuntime() {
                //DebugNetwork = true,
                MaxTime = 50.Sec()
            };
            
            sim.Net.Link("client", "api1");
            sim.Net.Link("client", "api2");
            sim.Net.Link("api1", "mv");
            sim.Net.Link("api2", "mv");
            
            sim.Net.TraceRoute("client", "api1");
            
            sim.Services.Add("mv", env => new CommitLog(env, 443).Run());

            sim.Services.Add(new[] {"api1", "api2"}, env => {
                var client = new CommitLogClient(env, "mv:443");
                return new Backend(env, 443, client).Run();
            });
            
            sim.Services.Add("client", env => {
                var client = new BackendClient(env, "api1:443", "api2:443");
                return new UserBot(env, 5,client).FireLoad();
            });
            
            sim.RunPlan(async plan => {
                plan.StartServices();
                await plan.Delay(8.Sec());
                plan.Debug("STOP api1");
                await plan.StopServices(s => s.Machine == "api1");
                await plan.Delay(10.Sec());
                plan.Debug("START api1");
                plan.StartServices(s => s.Machine == "api1");
            });
        }
    }
}