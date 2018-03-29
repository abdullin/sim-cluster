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

            var test = PlaygroundTests.InventoryMoverBotOver3GConnection();

            test.Run();

            var old = Console.ForegroundColor;
            Console.ForegroundColor = ConsoleColor.Red;
            
            foreach (var bot in test.Bots) {
                foreach (var issue in bot.Verify()) {
                    Console.WriteLine($"  {bot.GetType().Name} expected {issue.Field} to be {issue.Expected} but got {issue.Actual}");
                }
            }

            Console.ForegroundColor = old;

        }

        
    }
}
