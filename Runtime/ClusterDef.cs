using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

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
        
        



        
        
        public void LinkNets(string from, string to, TimeSpan?latency= null) {
            var def = new RouteDef {
                Latency = latency ?? 50.Ms()
            };
            Routes.Add(new RouteId(from, to), def);
            Routes.Add(new RouteId(to, from), def);
        }

        
       
    }
}