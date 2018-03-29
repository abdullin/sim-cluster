using System;
using System.Collections.Generic;
using SimMach.Sim;

namespace SimMach {
    
    
    public class RouteId {
        public readonly string Source;
        public readonly string Destinaton;
        public RouteId(string source, string destinaton) {
            Source = source;
            Destinaton = destinaton;
        }

        public string Full => $"{Source}->{Destinaton}";

        public override string ToString() {
            return Full;
        }

        public static readonly IEqualityComparer<RouteId> Comparer = new DelegateComparer<RouteId>(id => id.Full);
    }

    public class RouteDef {
        public Func<SimRandom,TimeSpan> Latency;
        public Func<SimRandom, bool> PacketLoss;
        public Predicate<SimPacket> Debug = packet => false;
        
    }
}