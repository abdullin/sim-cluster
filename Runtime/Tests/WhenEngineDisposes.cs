using System;
using System.Threading;
using System.Threading.Tasks;
using NUnit.Framework;

namespace SimMach.Sim {
    public sealed class WhenEngineDisposes {
        [Test]
        public void DisposeIsFiredOnNormalTermination() {
            var test = new TestDef();

            bool run = false;
            bool disposed = false;
            
            
            test.AddService("run", e => new TestEngine(
                async () => { run = true; },
                async () => { disposed = true;}
            ));
            
            test.Run();
            Assert.IsTrue(disposed, nameof(disposed));
            Assert.IsTrue(run, nameof(run));
        }
        
        
        [Test]
        public void DisposeIsFiredOnAbnormalTermination() {
            var runtime = new TestDef();

            bool run = false;
            bool disposed = false;
            
            
            runtime.AddService("run", e => new TestEngine(
                async () => {
                    run = true;
                    throw new InvalidOperationException();
                },
                async () => { disposed = true;}
            ));
            
            runtime.Run();
            Assert.IsTrue(disposed, nameof(disposed));
            Assert.IsTrue(run, nameof(run));
        }
        
        [Test]
        public void DisposeIsNotFiredOnKill() {
            var runtime = new TestDef();

            bool run = false;
            bool disposed = false;
            
            runtime.AddService("run", e => new TestEngine(
                async () => {
                    run = true;
                    await e.Delay(10.Minutes());
                },
                async () => { disposed = true;}
            ));
            
            runtime.Run(async e => {
                e.StartServices();
                await e.Delay(5.Sec());
                await e.StopServices(grace: 2.Sec());
            });
            Assert.IsFalse(disposed, nameof(disposed));
            Assert.IsTrue(run, nameof(run));
        }
        
        [Test]
        public void SlowDisposeIsKilled() {
            var test = new TestDef();

            
            bool disposeStart = false;
            bool disposeComplete = false;
            
            test.AddService("run", e => new TestEngine(
                async () => {
                    try {
                        await e.SimulateWork(Timeout.InfiniteTimeSpan, e.Token);
                    } catch (TaskCanceledException) {
                        
                    }

                    e.Debug("Shutting down");
                }, 
                async () => {
                    e.Debug("Disposing");
                    disposeStart = true;
                    await e.SimulateWork(10.Sec());
                    disposeComplete = true;
                }
            ));
            
            test.Run(async e => {
                e.StartServices();
                await e.Delay(5.Sec());
                e.Debug(LogType.Info,  "Start shutting down");
                await e.StopServices(grace: 2.Sec());
            });
            Assert.IsTrue(disposeStart, nameof(disposeStart));
            Assert.IsFalse(disposeComplete, nameof(disposeComplete));
            
        }



        sealed class TestEngine : IEngine {
            readonly Func<Task> _run;
            readonly Func<Task> _dispose;

            public TestEngine(Func<Task> run, Func<Task> dispose) {
                _run = run;
                _dispose = dispose;
            }

            public async Task Run() {
                await _run();
                
            }

            public async Task Dispose() {
                await _dispose();
                
            }
        }
    }
}