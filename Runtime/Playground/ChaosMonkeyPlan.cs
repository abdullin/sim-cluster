using System;
using System.Linq;
using System.Threading.Tasks;
using SimMach.Sim;

namespace SimMach.Playground {
    sealed class ChaosMonkeyPlan {

        public Func<string, bool> ApplyToMachines = s => true; 
        
        
        
        public  async Task Run(SimControl plan) {
            plan.StartServices();

            var deathPool = plan.Cluster.Machines
                .Select(p => p.Key)
                .Where(ApplyToMachines)
                .ToArray();
            
            plan.Debug($"Monkey has plans for {string.Join(", ", deathPool)}");

            while (true) {
                await plan.Delay(plan.Rand.Next(2, 5).Sec());

                var candidate = deathPool[plan.Rand.Next(0, deathPool.Length)];
                var grace = plan.Rand.Next(0, 5).Sec();
                var wipe = plan.Rand.Next(0, 3) == 1;

                if (wipe) {
                    plan.Debug($"KILL {candidate}");
                    await plan.StopServices(s => s.Machine == candidate, grace: grace);
                    plan.WipeStorage(candidate);
                } else {
                    plan.Debug($"REBOOT {candidate}");
                    await plan.StopServices(s => s.Machine == candidate, grace: grace);
                }

                await plan.Delay(plan.Rand.Next(2, 10).Sec());
                plan.Debug($"START {candidate}");
                plan.StartServices(s => s.Machine == candidate);
            }
        }
    }
}