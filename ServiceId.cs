using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace SimMach {
    class Topology : Dictionary<ServiceId, Func<IEnv, Task>> {
        public Topology() : base(new ServiceIdComparer()) { }
    }
    
    class ServiceId {

        public readonly string Full;
        public readonly string Machine;
        public readonly string Service;
        public readonly string Zone;
        

        public ServiceId(string input) {
            Full = input;
            
            var strings = input.Split(':');
            
            Service = strings[1];
            Machine = strings[0];
            
            var dparts = Machine.Split('.');


            Zone = dparts.Last();
        }

        
        public static implicit operator ServiceId(string input) {
            return new ServiceId(input);
        }


        public override string ToString() {
            return Full;
        }
    }
    
    sealed class ServiceIdComparer : IEqualityComparer<ServiceId> {
        public bool Equals(ServiceId x, ServiceId y) {
            return x.Full == y.Full;
        }

        public int GetHashCode(ServiceId obj) {
            return obj.Full.GetHashCode();
        }
    }
}