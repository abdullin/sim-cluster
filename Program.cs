using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Numerics;
using System.Reflection.Metadata.Ecma335;
using System.Threading;
using System.Threading.Tasks;
using SimMach.Playground;
using SimMach.Sim;

namespace SimMach
{

   
    class Program {
        static void Main(string[] args) {

            new PlaygroundTests().Playground();
        }

        static async Task QuickProcess(IEnv env) {
            env.Debug("Starting");
            try {
                while (!env.Token.IsCancellationRequested) {
                    await env.Delay(1.Sec(), env.Token);
                }
            } catch (TaskCanceledException) {
                env.Debug("Abort");
            }
            env.Debug("Shutting down");
        }
        static async Task SlowProcess(IEnv env) {
            env.Debug("Starting");
            try {
                while (!env.Token.IsCancellationRequested) {
                    await env.Delay(5.Sec(), env.Token);
                }
            } catch (TaskCanceledException) {
                env.Debug("Abort");
            }
            env.Debug("Shutting down");
        }
    }
}
