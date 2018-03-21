using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection.Emit;
using System.Threading.Tasks;

namespace SimMach.Sim {
    public sealed class TestRuntime {
        public readonly MachineDef Services = new MachineDef();
        public readonly NetworkDef Net = new NetworkDef();
        
        public TimeSpan MaxTime = TimeSpan.MaxValue;
        public long MaxSteps = long.MaxValue;
        public bool DebugNetwork;

        



        public void RunPlan(Func<ISimPlan, Task> plan) {
            var env = new SimRuntime(Services, Net) {
                MaxSteps = MaxSteps,
                MaxTime = MaxTime
            };
            
            
            env.Plan(plan);

            env.Network.DebugPackets = DebugNetwork;
            
            

            env.Run();
        }

        public void RunScript(Func<IEnv, Task> script) {
            Services.Add("local:script", script);
            RunAll();
        }


        public void RunAll() {
            RunPlan(async plan => plan.StartServices());
        }
    }
}