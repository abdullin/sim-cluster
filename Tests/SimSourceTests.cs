using System;
using System.Numerics;
using System.Threading;
using NUnit.Framework;

namespace SimMach.Sim {
    public sealed class SimSourceTests {
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
        
    }
}