using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using SimMach.Sim;

namespace SimMach {
    public class ClusterDef {

        public readonly Dictionary<ServiceId, Func<IEnv, IEngine>> Services =
            new Dictionary<ServiceId, Func<IEnv, IEngine>>(ServiceId.Comparer);


        public void AddService(string svc, Func<IEnv, IEngine> run) {
            if (!svc.Contains(':')) {
                var count = Services.Count(p => p.Key.Machine == svc);
                
                svc = svc + ":svc" + count;
            }
            Services.Add(new ServiceId(svc), run);
        }
        
        
      
        
        public readonly Dictionary<RouteId, RouteDef> Routes = new Dictionary<RouteId, RouteDef>(RouteId.Comparer);
        
        



        
        
        public void LinkNets(string from, string to, Action<RouteDef> cfg = null) {
            Routes.Add(new RouteId(from, to), MakeRouteDef(cfg));
            Routes.Add(new RouteId(to, from), MakeRouteDef(cfg));
        }

        static RouteDef MakeRouteDef(Action<RouteDef> cfg) {
            var def = new RouteDef {
                Latency = r => 50.Ms()
            };
            cfg?.Invoke(def);
            return def;
        }
    }
}