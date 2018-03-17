using System;
using System.IO;
using System.Numerics;
using System.Threading;
using NUnit.Framework;

namespace SimMach.Sim {
    public sealed class SimPromiseTests {
        [Test]
        public void CompletionSourceTimesOut() {
            var test = new TestRuntime() {
                MaxSteps = 100,
            };

            TimeSpan timedOut;
            
            test.Services.Add("m:m", async env => {
                
                // TODO: move promise gen to the env
                var promise = new SimCompletionSource<bool>(TimeSpan.FromSeconds(5));
                try {
                    await promise.Task;
                } catch (TimeoutException) {
                    timedOut = env.Time;
                }
            });
            
            test.RunAll();
            Assert.AreEqual(TimeSpan.FromSeconds(5), timedOut);
        }
        
        [Test]
        public void SettingResultSyncCompletesPromise() {
            var test = new TestRuntime() {
                MaxSteps = 100,
            };

            TimeSpan completed = TimeSpan.MinValue;
            bool result = false;
            
            test.Services.Add("m:m", async env => {
                
                // TODO: move promise gen to the env
                var promise = new SimCompletionSource<bool>(TimeSpan.FromSeconds(5));
                promise.SetResult(true);
                result = await promise.Task;
                completed = env.Time;

            });
            
            test.RunAll();
            Assert.IsTrue(result);
            Assert.AreEqual(TimeSpan.Zero, completed);
        }
        [Test]
        public void SettingErrorSyncCompletesPromise() {
            var test = new TestRuntime();

            var failed = TimeSpan.MinValue;
            var result = false;
            
            test.RunPlan(async env => {
                
                // TODO: move promise gen to the env
                var promise = new SimCompletionSource<bool>(TimeSpan.FromSeconds(5));
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

    }
}