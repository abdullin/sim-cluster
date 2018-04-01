using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using NUnit.Framework;
using SimMach.Playground.Backend;
using SimMach.Sim;

namespace SimMach.Playground {

    public sealed class InventoryMoverBot : IBot {

        public int RingSize = 5;
        public string[] Servers;
        public int Iterations = 5;
        public TimeSpan Delay = 1.Sec();
        public bool HaltOnCompletion = false;
        ushort Port = 443;

        decimal _actualCount;
        decimal _expectedCount;

        public InventoryMoverBot(params string[] servers) {
            Servers = servers;
        }


        async Task Run(IEnv env) {

            _expectedCount = 0M;
            var endpoints = Servers.Select(m => new SimEndpoint(m, Port)).ToArray();
            
            var lib = new BackendClient(env, endpoints);
        
            await lib.AddItem(0, 1);

            _expectedCount = 1M;

            try {
                for (int i = 0; i < Iterations; i++) {
                    var curr = i % RingSize;
                    var next = (i + 1) % RingSize;
                    await lib.MoveItem(curr, next, 1);
                    await env.Delay(Delay);
                }
            } catch (ArgumentException ex) {
                env.Error("Server invariant violated", ex);
            }

            _actualCount = await lib.Count();
            if (HaltOnCompletion) {
                env.Halt("DONE");
            }
        }

        public IList<BotIssue> Verify() {
            var botIssues = new List<BotIssue>();
            
            if (_actualCount != _expectedCount) {
                botIssues.Add(new BotIssue("finalCount", _expectedCount, _actualCount));
            }
            return botIssues;
            
        }

        public IEngine Engine(IEnv e) {
            return new LambdaEngine(Run(e));
        }
    }
}