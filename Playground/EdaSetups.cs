using NUnit.Framework;

namespace SimMach.Sim {
    public sealed class EdaSetups {


        [Test]
        public void Playground() {
            
            
            var sim = new TestRuntime() {
                
                //DebugNetwork = true
            };
            
            
            
            sim.Net.Link("client", "api");
            sim.Net.Link("api", "mv");
            
            sim.Net.TraceRoute("api", "mv");
            
            
            sim.Services.Add("mv", env => new CommitLog(env, 443).Run());
            sim.Services.Add("api", env => {
                var client = new CommitLogClient(env, new SimEndpoint("mv", 443));
                return new Backend(env, 443, client).Run();
            });
            
            sim.Services.Add("client", env => {
                var client = new BackendClient(env, new SimEndpoint("api", 443));
                return new UserBot(env, 5,client).FireLoad();
            });
            
            sim.RunPlan(async plan => {
                plan.StartServices();
                await plan.Delay(9.Sec());
                plan.Debug("Reboot the API");
                await plan.StopServices(s => s.Machine == "api");
                plan.StartServices(s => s.Machine == "api");

            });
            
            
        }
        
        
    }
}