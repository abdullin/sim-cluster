using System;
using System.Collections.Generic;

namespace SimMach {
    
    
    public class RouteId {
        public readonly string Client;
        public readonly string Server;
        public RouteId(string client, string server) {
            Client = client;
            Server = server;
        }

        public string Full => $"{Client}->{Server}";

        public override string ToString() {
            return Full;
        }

        public static readonly IEqualityComparer<RouteId> Comparer = new DelegateComparer<RouteId>(id => id.Full);
    }

    public class RouteCfg {
        public int LatencyMs = 50;
    }
}