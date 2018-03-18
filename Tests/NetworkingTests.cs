using System;
using System.Runtime.InteropServices.ComTypes;
using NUnit.Framework;

namespace SimMach.Sim {
    public sealed class NetworkingTests {
        [Test]
        public void RequestReplyTest() {
            var run = new TestRuntime() {
                MaxTime = TimeSpan.FromMinutes(2),
                DebugNetwork = true,
            };

            object request = null;
            object response = null;

            run.Net.Link("client", "server");

            run.Services.Add("client:service", async env => {
                using (var conn = await env.Connect("server", 80)) {
                    await conn.Write("Hello");
                    response = await conn.Read();
                }
            });

            run.Services.Add("server:service", async env => {
                using (var conn = await env.Listen(80)) {
                    request = await conn.Read();
                    await conn.Write("World");
                }
            });

            run.RunAll();

            Assert.AreEqual("Hello", request);
            Assert.AreEqual("World", response);
        }
        
        
        
        [Test]
        public void SubscribeTest() {
            var run = new TestRuntime() {
                MaxTime = 2.Minutes(),
                DebugNetwork = true,
            };


            int eventsReceived = 0;
            int eventsToSend = 5;
            bool closed = false;
            run.Net.Link("client", "server");

            run.Services.Add("client:service", async env => {
                using (var conn = await env.Connect("server", 80)) {
                    await conn.Write("Subscribe");

                    using (var stream = conn.ReadStream()) {
                        while (await stream.MoveNext()) {
                            eventsReceived++;
                        }
                    }

                    closed = true;
                }
            });

            run.Services.Add("server:service", async env => {
                using (var conn = await env.Listen(80)) {
                    await conn.Read(5.Sec());

                    for (int i = 0; i < eventsToSend; i++) {
                        await env.Delay(750, env.Token);
                        await conn.Write($"Event {i}"); 
                    }

                    await conn.Write(new SimHeaders() {
                        EndStream = true
                    });

                }
            });

            run.RunAll();

            Assert.AreEqual(eventsToSend, eventsReceived);
            Assert.IsTrue(closed, nameof(closed));
        }
    }
}