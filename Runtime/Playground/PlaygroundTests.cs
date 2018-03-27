using System;
using NUnit.Framework;
using SimMach.Playground.Backend;
using SimMach.Playground.CommitLog;
using SimMach.Sim;

namespace SimMach.Playground {
    public sealed class PlaygroundTests {


        [Test]
        public void Playground() {
            var test = new TestDef();
            
            test.LinkNets("bot", "public");
            test.LinkNets("public", "internal");

            test.AddService("cl.internal", InstallCommitLog());
            test.AddService("api1.public", InstallBackend("cl.internal"));
            test.AddService("api2.public", InstallBackend("cl.internal"));

            var bot = new RingMoverBot("api1.public", "api2.public") {
                RingSize = 5,
                Iterations = 10,
                Delay = 5.Sec(),
                HaltOnCompletion = true
            };
            
            test.AddScript("bot", bot.Run);
            
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
            
            bot.Verify();
        }
        
        
        [Test]
        public void PlaygroundFuzzy() {
            var test = new TestDef();
            
            test.LinkNets("bot", "public", NetworkPresets.Mobile3G);
            test.LinkNets("public", "internal", NetworkPresets.Intranet);

            test.AddService("cl.internal", InstallCommitLog());
            test.AddService("api1.public", InstallBackend("cl.internal"));
            test.AddService("api2.public", InstallBackend("cl.internal"));

            var bot = new RingMoverBot("api1.public", "api2.public") {
                RingSize = 5,
                Iterations = 10,
                Delay = 5.Sec(),
                HaltOnCompletion = true
            };
            
            test.AddScript("bot", bot.Run);
            
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
            
            bot.Verify();
        }

        static Func<IEnv, IEngine> InstallCommitLog() {
            return env => new CommitLogServer(env, 443);
        }

        static Func<IEnv, IEngine> InstallBackend(string cl) {
            return env => {
                var client = new CommitLogClient(env, cl + ":443");
                return new BackendServer(env, 443, client);
            };
        }
    }
}