using System;

namespace SimMach.Playground {
    public static class NetworkProfile {
        public static void Internet(RouteDef obj) {
            obj.Latency = r => {
                var next = r.Next(20, 100);
                Console.WriteLine($"Latency {next} ms");
                return next.Ms();
            };
        }

        public static void AzureIntranet(RouteDef obj) {
            obj.Latency = r => r.Next(2, 10).Ms();
        }

        public static void IdealFixed(RouteDef def) {
            def.Latency = random => 50.Ms();
        }

        public static void Mobile3G(RouteDef def) {
            // TODO: use Zigorat or Box Muller transform
            // for better latencies
            def.Latency = r => r.Next(100, 500).Ms();
            def.PacketLoss = r => r.Next(0, 100) == 1;
        }

        public static void ReverseLatency(RouteDef def) {
            // from 50 to 10 and back
            int latency = 50;
            def.Latency = r => {
                latency -= 2;
                if (latency <= 10) {
                    latency = 50;
                }

                return latency.Ms();
            };
        }


        public static void LogAll(RouteDef d) {
            d.Debug = packet => true;
        }

        public static Action<RouteDef> LogMachine(string message) {
            
            return def => {
                def.Debug = packet => packet.Source.Machine == message || packet.Destination.Machine == message;
            };
        }
        
       
        
        /*

         *
from: https://serverfault.com/a/573815/10189
Generation | Data rate      | Latency
2G         | 100–400 Kbit/s | 300–1000 ms
3G         | 0.5–5 Mbit/s   | 100–500 ms
4G         | 1–50 Mbit/s    | < 100 ms
         */
    }
}