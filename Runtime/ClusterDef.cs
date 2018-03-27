using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace SimMach {
    public class ClusterDef {

        public readonly Dictionary<ServiceId, Func<IEnv, IEngine>> Services =
            new Dictionary<ServiceId, Func<IEnv, IEngine>>(ServiceId.Comparer);


        public void Add(string svc, Func<IEnv, IEngine> run) {
            if (!svc.Contains(':')) {
                svc = svc + ":" + svc;
            }
            Services.Add(new ServiceId(svc), run);
        }
        
        
        public void Add(string svc, Func<IEnv, Task> run) {
            Add(svc, env => new LambdaEngine(run(env)));
        }
        
        public readonly Dictionary<RouteId, RouteDef> Routes = new Dictionary<RouteId, RouteDef>(RouteId.Comparer);
        
        



        
        
        public void Link(string from, string to) {
            var cfg = new RouteDef();
            Routes.Add(new RouteId(from, to), cfg);
            Routes.Add(new RouteId(to, from), cfg);
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
    }
}