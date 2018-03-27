using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Threading.Tasks;

namespace SimMach {
    public class ClusterDef {

        public readonly Dictionary<ServiceId, Func<IEnv, IEngine>> Dictionary =
            new Dictionary<ServiceId, Func<IEnv, IEngine>>(ServiceId.Comparer);


        public void Add(string svc, Func<IEnv, IEngine> run) {
            if (!svc.Contains(':')) {
                svc = svc + ":" + svc;
            }
            Dictionary.Add(new ServiceId(svc), run);
        }
        
        
        public void Add(string svc, Func<IEnv, Task> run) {
            Add(svc, env => new LambdaEngine(run(env)));
        }

        

        sealed class LambdaEngine : IEngine {
            readonly Task _func;


            public LambdaEngine(Task func) {
                _func = func;
            }

            public Task Run() {
               return _func;
            }

            public Task Dispose() {
                return Task.CompletedTask;
            }
        }




        /* public void PrettyPrint() {
             var machines = this.GroupBy(p => p.Key.Machine);
             // printing
             foreach (var m in machines) {
                 Console.WriteLine($"{m.Key}");
 
                 foreach (var svc in m) {
                     Console.WriteLine($"  {svc.Key.Service}");
                 }
             }
         }*/
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