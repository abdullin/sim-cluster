using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using NUnit.Framework;
using SimMach.Playground;

namespace SimMach.Sim {
    public sealed class ScenarioDef : ClusterDef {
        public TimeSpan MaxTime = TimeSpan.MaxValue;
        public TimeSpan? MaxInactive = TimeSpan.MaxValue;
        public long MaxSteps = long.MaxValue;

        public Func<SimControl, Task> Plan = async control => control.StartServices();


        public void Run() {
            var env = new SimRuntime(this) {
                MaxSteps = MaxSteps,
                MaxTime = MaxTime
            };

            if (MaxInactive.HasValue) {
                env.MaxInactive = MaxInactive.Value;
            }

            env.Run(Plan);
        }
        
        public readonly IList<IBot> Bots = new List<IBot>();


        public void AddBot(IBot bot) {
            var name = $"{Bots.Count}.botnet";
            Bots.Add(bot);
            AddService(name, bot.Engine);
        }

        public void Assert() {
            Run();
            foreach (var bot in Bots) {
                CollectionAssert.IsEmpty(bot.Verify(), "There should be no bot issues");
            }
        }
    }
}