using System;
using System.Collections.Generic;

namespace SimMach {
    public sealed class NetworkDef {
        readonly Dictionary<RouteId, RouteCfg> _dictionary = new Dictionary<RouteId, RouteCfg>(RouteId.Comparer);
        
        

        public ICollection<RouteId> Links => _dictionary.Keys;

        public HashSet<RouteId> DebugRoutes = new HashSet<RouteId>(RouteId.Comparer);
        
        public void Link(params string[] server) {
            foreach (var from in server) {
                foreach (var to in server) {
                    if (from != to) {
                        _dictionary.Add(new RouteId(from, to), new RouteCfg());
                    }
                }
            }
        }


        public void TraceRoute(string client, string server) {
            DebugRoutes.Add(new RouteId(client, server));
            DebugRoutes.Add(new RouteId(server, client));
        }
        
    }
    
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