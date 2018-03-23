using System.Collections.Generic;
using System.Threading.Tasks;
using SimMach.Sim;

namespace SimMach.Playground.CommitLog {
    public sealed class CommitLogClient {
        readonly IEnv _env;
        readonly SimEndpoint _endpoint;

        public CommitLogClient(IEnv env, SimEndpoint endpoint) {
            _env = env;
            _endpoint = endpoint;
        }

        public async Task Commit(params object[] e) {
            _env.Debug($"Commit '{string.Join(", ", e)}' to {_endpoint}");
            using (var conn = await _env.Connect(_endpoint)) {
                await conn.Write(new CommitRequest(e));
                await conn.Read(5.Sec());
            }
        }

        public async Task<IList<object>> Read(int from, int count) {
            using (var conn = await _env.Connect(_endpoint)) {
                await conn.Write(new DownloadRequest(from, count));
                var result = await conn.Read(5.Sec());
                return (IList<object>) result;
            }
        }
    }
}