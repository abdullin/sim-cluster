using System.Collections.Generic;
using System.Linq;

namespace SimMach {
    class ServiceName {

        public readonly string Full;
        public readonly string Machine;
        public readonly string Service;
        public readonly string Zone;
        

        public ServiceName(string input) {
            Full = input;
            
            var strings = input.Split(':');
            
            Service = strings[1];
            Machine = strings[0];
            
            var dparts = Machine.Split('.');


            Zone = dparts.Last();
        }

        
        public static implicit operator ServiceName(string input) {
            return new ServiceName(input);
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