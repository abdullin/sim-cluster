using System.Collections.Generic;

namespace SimMach {
    class ServiceName {

        public readonly string Full;
        public readonly string Machine;
        public readonly string Service;

        public ServiceName(string full, string machine, string service) {
            Full = full;
            Machine = machine;
            Service = service;
        }

        public static ServiceName Parse(string input) {
            var strings = input.Split(':');
            var svc = strings[1];
            var machine = strings[0];
            
            return new ServiceName(input, machine, svc);
        }
        
        public static implicit operator ServiceName(string input) {
            return Parse(input);
        }


        public override string ToString() {
            return Full;
        }
    }
    
    sealed class ServiceNameComparer : IEqualityComparer<ServiceName> {
        public bool Equals(ServiceName x, ServiceName y) {
            return x.Full == y.Full;
        }

        public int GetHashCode(ServiceName obj) {
            return obj.Full.GetHashCode();
        }
    }
}