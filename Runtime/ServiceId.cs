using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;

namespace SimMach {
    public class ServiceId {

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


            Zone = dparts.Length == 1 ? Machine : string.Join('.', dparts.Skip(1));
        }

        
        public static implicit operator ServiceId(string input) {
            return new ServiceId(input);
        }


        public override string ToString() {
            return Full;
        }

        public static IEqualityComparer<ServiceId> Comparer = new DelegateComparer<ServiceId>(id => id.Full);
    }
    
 
    sealed class DelegateComparer<T> : IEqualityComparer<T> where T : class {
        readonly Func<T, object> _selector;
        public DelegateComparer(Func<T, object> selector) {
            _selector = selector;
        }

        public bool Equals(T x, T y) {
            return _selector(x).Equals(_selector(y));
        }

        public int GetHashCode(T obj) {
            return _selector(obj).GetHashCode();
        }
    }
}