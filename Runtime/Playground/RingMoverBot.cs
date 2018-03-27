using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using NUnit.Framework;
using SimMach.Playground.Backend;
using SimMach.Sim;

namespace SimMach.Playground {
    public sealed class RingMoverBot {

        public int RingSize = 5;
        public string[] Machines;
        public int Iterations = 5;
        public TimeSpan Delay = 1.Sec();
        public bool HaltOnCompletion = false;
        public ushort Port = 443;

        decimal _finalCount;


        public RingMoverBot(params string[] machines) {
            Machines = machines;
        }


        public async Task Run(IEnv env) {

            var endpoints = Machines.Select(m => new SimEndpoint(m, Port)).ToArray();
            
      
            
            var lib = new BackendClient(env, endpoints);
        
            await lib.AddItem(0, 1);
                
            for (int i = 0; i < Iterations; i++) {
                var curr = i % RingSize;
                var next = (i + 1) % RingSize;
                await lib.MoveItem(curr, next, 1);
                await env.Delay(Delay);
            }
            _finalCount = await lib.Count();
            if (HaltOnCompletion) {
                env.Halt("DONE");
            }
        }

        public void Verify() {
            Assert.AreEqual(1M, _finalCount, nameof(_finalCount));
        }

        
    }
}