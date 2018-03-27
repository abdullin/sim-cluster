using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection.Emit;
using System.Threading.Tasks;

namespace SimMach.Sim {
    sealed class TestDef : ClusterDef {
        public TimeSpan MaxTime = TimeSpan.MaxValue;
        public TimeSpan? MaxInactive = TimeSpan.MaxValue;
        public long MaxSteps = long.MaxValue;
      



        public void RunPlan(Func<SimControl, Task> plan) {
            var env = new SimRuntime(this) {
                MaxSteps = MaxSteps,
                MaxTime = MaxTime
            };

            if (MaxInactive.HasValue) {
                env.MaxInactive = MaxInactive.Value;
            }

            env.Run(plan);
        }

        public void RunScript(Func<IEnv, Task> script) {
            AddScript("local:script", script);
            RunAll();
        }


        public void RunAll() {
            RunPlan(async plan => plan.StartServices());
        }
        
        
        public void AddScript(string svc, Func<IEnv, Task> run) {
            AddService(svc, env => new LambdaEngine(run(env)));
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
}