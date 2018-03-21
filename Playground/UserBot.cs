using System;
using System.Threading.Tasks;

namespace SimMach.Sim {
    class UserBot  {
        readonly IEnv _sim;
        readonly int _count;
        readonly BackendClient _client;

        public UserBot(IEnv sim, int count, BackendClient client) {
            _sim = sim;
            _count = count;
            _client = client;
        }


        public async Task FireLoad() {
            for (int i = 0; i < _count; i++) {

                await _sim.Delay(10.Sec());
                await _client.AddItem(1, 10);
            }
            // we don't want to restart, so we wait forever
            await _sim.Delay(TimeSpan.FromDays(10));
        }

      
        
        
    }
}