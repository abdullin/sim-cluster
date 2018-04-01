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
        
        



        
        
        public void Connect(string from, string to, params Action<RouteDef>[] cfg) {
            Routes.Add(new RouteId(from, to), MakeRouteDef(cfg));
            Routes.Add(new RouteId(to, from), MakeRouteDef(cfg));
        }

        static RouteDef MakeRouteDef(params Action<RouteDef>[] cfg) {
            var def = new RouteDef {
                Latency = r => 50.Ms(),
                Debug = packet => false,
                LogFaults = true,
            };
            foreach (var c in cfg) {
                c(def);
            }
            
            
            return def;
        }
    }
}