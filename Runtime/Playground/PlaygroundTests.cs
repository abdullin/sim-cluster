using System;
using System.Linq;
using System.Threading.Tasks;
using NUnit.Framework;
using SimMach.Playground.Backend;
using SimMach.Playground.CommitLog;
using SimMach.Sim;

namespace SimMach.Playground {
    public sealed class PlaygroundTests {


        [Test]
        public void Playground() {
            var test = new TestDef();
            
            test.Connect("bot", "public");
            test.Connect("public", "internal");

            test.AddService("cl.internal", InstallCommitLog);
            test.AddService("api1.public", InstallBackend("cl.internal"));
            test.AddService("api2.public", InstallBackend("cl.internal"));

            var bot = new InventoryMoverBot("api1.public", "api2.public") {
                RingSize = 5,
                Iterations = 10,
                Delay = 5.Sec(),
                HaltOnCompletion = true
            };
            
            test.AddScript("bot", bot.Run);
            
            test.Run(async plan => {
                plan.StartServices();
                await plan.Delay(6.Sec());
                plan.Debug("REIMAGE api1");
                await plan.StopServices(s => s.Machine == "api1.public", grace:1.Sec());
                plan.WipeStorage("api1");
                await plan.Delay(2.Sec());
                plan.Debug("START api1");
                plan.StartServices(s => s.Machine == "api1.public");
            });
            
            bot.Verify();
        }



        public static TestDef InventoryMoverBotOver3GConnection() {
            var test = new TestDef();
            
            test.Connect("botnet", "public", NetworkProfile.Mobile3G);
            test.Connect("public", "internal", NetworkProfile.AzureIntranet);

            test.AddService("cl.internal", InstallCommitLog);
            test.AddService("api1.public", InstallBackend("cl.internal"));
            test.AddService("api2.public", InstallBackend("cl.internal"));

            var mover = new InventoryMoverBot("api1.public", "api2.public") {
                RingSize = 5,
                Iterations = 10,
                Delay = 5.Sec(),
                HaltOnCompletion = true
            };
            
            test.AddBot(mover);
            
            var monkey = new ChaosMonkeyPlan {
                ApplyToMachines = s => s.StartsWith("api")
            };

            test.Plan = monkey.Run;
            return test;
        }
        
        
        
        [Test]
        public void InventoryMoverBotOver3GConnectionTest() {
            Verify.NUnit(InventoryMoverBotOver3GConnection());
            
        }


        static Func<IEnv, IEngine> InstallBackend(string cl) {
            return env => {
                var client = new CommitLogClient(env, cl + ":443");
                return new BackendServer(env, 443, client);
            };
        }

        static IEngine InstallCommitLog(IEnv env) {
            return new CommitLogServer(env, 443);
        }
    }
    
    
  
    
    
    
}