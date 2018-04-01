using System.Collections.Generic;

namespace SimMach.Sim {
    public sealed class SimEndpoint {
        public readonly string Machine;
        public readonly ushort Port;
        
        public SimEndpoint(string machine, ushort port) {
            Machine = machine;
            Port = port;
        }

        public override string ToString() {
            return $"{Machine}:{Port}";
        }

        public static readonly IEqualityComparer<SimEndpoint> Comparer = new DelegateComparer<SimEndpoint>(a => a.ToString());

        public static implicit operator SimEndpoint(string addr) {
            var args = addr.Split(":");
            return new SimEndpoint(args[0], ushort.Parse(args[1]));
        }
    }
}