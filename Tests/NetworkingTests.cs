using System;
using NUnit.Framework;

namespace SimMach.Sim {
    public sealed class NetworkingTests {
        [Test]
        public void RequestReplyTest() {
            var run = new TestRuntime() {
                MaxTime = TimeSpan.FromMinutes(2)
            };

            object request = null;
            object response = null;

            run.Net.Link("client", "server");

            run.Services.Add("client:", async env => {
                using (var conn = await env.Connect("server",80)) {
                    await conn.Write("Hello");
                    response = await conn.Read();
                }
            });
            
            run.Services.Add("server:", async env => {
                using (var conn = await env.Listen(80)) {
                    request = await conn.Read();
                    await conn.Write("World");
                }
            });
            
            run.RunAll();
            
            Assert.AreEqual("Hello", request);
            Assert.AreEqual("World", response);
        }
    }
}