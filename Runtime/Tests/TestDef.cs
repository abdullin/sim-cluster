using System;
using System.IO;
using System.Reflection.Emit;
using System.Threading.Tasks;
using System.Transactions;
using NUnit.Framework;

namespace SimMach.Sim {
    public sealed class TestDef : ClusterDef {
        public TimeSpan MaxTime = TimeSpan.MaxValue;
        public TimeSpan? MaxInactive = TimeSpan.MaxValue;
        public long MaxSteps = long.MaxValue;

        readonly Func<SimControl, Task> Plan = async control => control.StartServices();


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
        
        public void AddScript(string svc, Func<IEnv, Task> run) {
            AddService(svc, env => new LambdaEngine(run(env)));
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