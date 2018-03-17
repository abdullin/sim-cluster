using System;
using System.IO;
using System.Numerics;
using System.Threading;
using System.Threading.Tasks;
using NUnit.Framework;

namespace SimMach.Sim {
    public sealed class SimPromiseTests {
        [Test]
        public void CompletionSourceTimesOut() {
            var test = new TestRuntime() {
                MaxSteps = 100,
            };

            TimeSpan timedOut;
            
            test.RunScript(async env => {
                var promise = new SimFuture<bool>(5000);
                try {
                    await promise.Task;
                } catch (TimeoutException) {
                    timedOut = env.Time;
                }
            });
            
            
            Assert.AreEqual(TimeSpan.FromSeconds(5), timedOut);
        }
        
        [Test]
        public void SettingResultSyncCompletesPromise() {
            var test = new TestRuntime() {
                MaxSteps = 100,
            };

            var completed = TimeSpan.MinValue;
            bool result = false;
            
            test.RunScript(async env => {
                var promise = new SimFuture<bool>(5000);
                promise.SetResult(true);
                result = await promise.Task;
                completed = env.Time;

            });
            
            Assert.IsTrue(result);
            Assert.AreEqual(TimeSpan.Zero, completed);
        }
        [Test]
        public void SettingErrorSyncCompletesPromise() {
            var test = new TestRuntime();

            var failed = TimeSpan.MinValue;
            var result = false;
            
            test.RunScript(async env => {
                var promise = new SimFuture<bool>(5000);
                promise.SetError(new IOException());
                try {
                    result = await promise.Task;
                } catch (Exception) {
                    failed = env.Time;    
                }
            });
            
            
            Assert.IsFalse(result);
            Assert.AreEqual(TimeSpan.Zero, failed);
        }
        
        [Test]
        public void CancellationAbortsPromise() {
            var test = new TestRuntime();

            var cancel = TimeSpan.MinValue;
            var result = false;
            
            test.Services.Add("m:m", async env => {
                var promise = new SimFuture<bool>(5000, env.Token);
                try {
                    result = await promise.Task;
                } catch (TaskCanceledException) {
                    cancel = env.Time;
                }
            });
            
            test.RunPlan(async plan => {
                plan.StartServices();
                await plan.Delay(1000);
                await plan.StopServices();
            });
            
            
            Assert.IsFalse(result);
            Assert.AreEqual(TimeSpan.FromMilliseconds(1000), cancel);
        }

    }
}