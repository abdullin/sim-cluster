using System;
using System.Reflection.Metadata.Ecma335;
using System.Threading.Tasks;
using NUnit.Framework;

namespace SimMach.Sim {
    public sealed class RuntimeTests {
        [Test]
        public void RebootTest() {
            var bootCount = 0;

            var test = new TestRuntime() {
                MaxTime = 2.Minutes(),
                MaxInactive = 2.Minutes()
            };

            test.Svc.Add("com:test", async env => {
                bootCount++;
                while (!env.Token.IsCancellationRequested) {
                    await env.SimulateWork(100.Ms());
                }
            });
            
            test.RunPlan(async plan => {
                plan.StartServices();
                await plan.Delay(TimeSpan.FromMinutes(1));
                await plan.StopServices();
                plan.StartServices();
            });

            Assert.AreEqual(2, bootCount);
        }

        [Test]
        public void NonResponsiveServiceIsKilledWithoutMercy() {
            var test = new TestRuntime {
                MaxTime = TimeSpan.FromMinutes(1)
            };

            var launched = true;
            var terminated = false;

            test.Svc.Add("com:test", async env => {
                launched = true;
                try {
                    while (!env.Token.IsCancellationRequested) {
                        await env.SimulateWork(10000.Ms());
                    }
                } finally {
                    // this should never hit
                    terminated = true;
                }
            });
            
            test.RunPlan(async plan => {
                plan.StartServices();
                await plan.Delay(1000);
                await plan.StopServices(grace:1.Sec());
            });
            
            Assert.IsTrue(launched, nameof(launched));
            Assert.IsFalse(terminated, nameof(terminated));
        }
        
        [Test]
        public void StopResponsiveService() {
            var test = new TestRuntime {
                MaxTime = TimeSpan.FromMinutes(1)
            };

            
            var terminated = TimeSpan.Zero;

            test.Svc.Add("com:test", async env => {
                try {
                    while (!env.Token.IsCancellationRequested) {
                        await env.SimulateWork(10.Sec(), env.Token);
                    }
                } finally {
                    terminated = env.Time;
                }
            });
            
            test.RunPlan(async plan => {
                plan.StartServices();
                await plan.Delay(TimeSpan.FromSeconds(1));
                await plan.StopServices();
            });
            
            Assert.AreEqual(TimeSpan.FromSeconds(1), terminated);
        }

       
    }
}