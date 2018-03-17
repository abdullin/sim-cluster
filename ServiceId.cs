using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Threading.Tasks;

namespace SimMach {
    public class MachineDef : Dictionary<ServiceId, Func<IEnv, Task>> {
        public MachineDef() : base(ServiceId.Comparer) { }



        public void PrettyPrint() {
            var machines = this.GroupBy(p => p.Key.Machine);
            // printing
            foreach (var m in machines) {
                Console.WriteLine($"{m.Key}");

                foreach (var svc in m) {
                    Console.WriteLine($"  {svc.Key.Service}");
                }
            }
        }
    }
    
    public sealed class NetworkDef {
        readonly Dictionary<LinkId, string> _dictionary = new Dictionary<LinkId, string>(LinkId.Comparer);

        public ICollection<LinkId> Links => _dictionary.Keys;

        public void Link(string client, string server) {
            _dictionary.Add(new LinkId(client, server), null);
        }
        
        
    }

    public class LinkId {
        public readonly string Client;
        public readonly string Server;
        public LinkId(string client, string server) {
            Client = client;
            Server = server;
        }

        public string Full => $"{Client}->{Server}";

        public override string ToString() {
            return Full;
        }

        public static readonly IEqualityComparer<LinkId> Comparer = new DelegateComparer<LinkId>(id => id.Full);
    }
    
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


            Zone = dparts.Last();
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