using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection.Emit;
using System.Threading.Tasks;
using System.Transactions;
using NUnit.Framework;
using SimMach.Playground;

namespace SimMach.Sim {
    public sealed class TestDef : ClusterDef {
        public TimeSpan MaxTime = TimeSpan.MaxValue;
        public TimeSpan? MaxInactive = TimeSpan.MaxValue;
        public long MaxSteps = long.MaxValue;

        public Func<SimControl, Task> Plan = async control => control.StartServices();


        public void Run(Func<SimControl, Task> plan = null) {
            var env = new SimRuntime(this) {
                MaxSteps = MaxSteps,
                MaxTime = MaxTime
            };

            if (MaxInactive.HasValue) {
                env.MaxInactive = MaxInactive.Value;
            }

            env.Run(plan ?? Plan);
        }

        public void RunScript(Func<IEnv, Task> script) {
            AddScript("local:script", script);
            Run();
        }


        
        
        public IList<IBot> Bots = new List<IBot>();


        public void AddBot(IBot bot) {
            var name = $"{Bots.Count}.botnet";
            Bots.Add(bot);
            AddService(name, bot.Engine);
        }
        
        public void AddScript(string svc, Func<IEnv, Task> run) {
            AddService(svc, env => new LambdaEngine(run(env)));
        }


       
       
    }


     static class Verify {
        public static void NUnit(TestDef def) {
            def.Run();
            foreach (var bot in def.Bots) {
                CollectionAssert.IsEmpty(bot.Verify(), "There should be no bot issues");
            }
        }
    }
    
    sealed class LambdaEngine : IEngine {
        readonly Task _func;


        public LambdaEngine(Task func) {
            _func = func;
        }

        public Task Run() {
            return _func;
        }

        public Task Dispose() {
            return Task.CompletedTask;
        }
    }
}