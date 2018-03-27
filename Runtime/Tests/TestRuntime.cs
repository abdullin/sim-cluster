using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection.Emit;
using System.Threading.Tasks;

namespace SimMach.Sim {
    public sealed class TestRuntime {
        public readonly ClusterDef Def = new ClusterDef();
        
        
        public TimeSpan MaxTime = TimeSpan.MaxValue;
        public TimeSpan? MaxInactive = TimeSpan.MaxValue;
        public long MaxSteps = long.MaxValue;
        public bool DebugNetwork;

        



        public void RunPlan(Func<ISimPlan, Task> plan) {
            var env = new SimRuntime(Def) {
                MaxSteps = MaxSteps,
                MaxTime = MaxTime
            };

            if (MaxInactive.HasValue) {
                env.MaxInactive = MaxInactive.Value;
            }
            
            
            env.Plan(plan);

            env.Network.DebugPackets = DebugNetwork;
            
            

            env.Run();
        }

        public void RunScript(Func<IEnv, Task> script) {
            Def.Add("local:script", script);
            RunAll();
        }


        public void RunAll() {
            RunPlan(async plan => plan.StartServices());
        }
    }
}