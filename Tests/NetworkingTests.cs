using System;
using System.Collections.Generic;
using System.Runtime.InteropServices.ComTypes;
using System.Threading.Tasks;
using NUnit.Framework;

namespace SimMach.Sim {
    public sealed class NetworkingTests {
        [Test]
        public void RequestReplyTest() {
            var run = new TestRuntime() {
                MaxTime = 2.Minutes(),
                DebugNetwork = true,
                TraceFile = "/Users/rinat/proj/core/SimMach/traces/trace.json"
            };

            var requests = new List<object>();
            var responses = new List<object>();

            run.Net.Link("localhost", "api");

            run.Services.Add("localhost:console", async env => {
                using (var conn = await env.Connect("api", 80)) {
                    env.Debug("Running");
                    await conn.Write("Hello");
                    var response = await conn.Read(5.Sec());
                    responses.Add(response);
                }
            });

            

            run.Services.Add("api:engine", async env => {
                async void Handler(IConn conn) {
                    using (conn) {
                        var msg = await conn.Read(5.Sec());
                        requests.Add(msg);
                        await conn.Write("World");
                    }
                }
                using (var socket = await env.Bind(80)) {
                    while (!env.Token.IsCancellationRequested) {
                        var conn = await socket.Accept();
                        Handler(conn);
                    }
                }
                
            });

            run.RunAll();

            CollectionAssert.AreEquivalent(new object[]{"Hello"}, requests);
            CollectionAssert.AreEquivalent(new object[]{"World"}, responses);
            
        }
        
        
        
        [Test]
        public void SubscribeTest() {
            var run = new TestRuntime() {
                MaxTime = 2.Minutes(),
                DebugNetwork = true,
                TraceFile = "/Users/rinat/proj/core/SimMach/traces/trace.json"
            };

            var eventsReceived = 0;
            var eventsToSend = 5;
            var closed = false;
            
            run.Net.Link("localhost", "api");
            run.Services.Add("localhost:console", async env => {
                using (var conn = await env.Connect("api", 80)) {
                    await conn.Write("SUBSCRIBE");
                    while (!env.Token.IsCancellationRequested) {
                        var msg = await conn.Read(5.Sec());
                        if (msg == "END_STREAM") {
                            env.Debug("End of stream");
                            break;
                        }
                        env.Debug($"Got {msg}");
                        eventsReceived++;
                    }
                    closed = true;
                }
            });

            run.Services.Add("api:engine", async env => {
                async void Handler(IConn conn) {
                    using (conn) {
                        await conn.Read(5.Sec());
                        for (var i = 0; i < eventsToSend; i++) {
                            await env.SimulateWork("work", 10.Ms());
                            await conn.Write($"Event {i}");
                        }
                        await conn.Write("END_STREAM");
                    }
                }
                
                using (var socket = await env.Bind(80)) {
                    while (!env.Token.IsCancellationRequested) {
                        var conn = await socket.Accept();
                        Handler(conn);
                    }
                }
            });

            run.RunAll();

            Assert.AreEqual(eventsToSend, eventsReceived);
            Assert.IsTrue(closed, nameof(closed));
        }
    }
}