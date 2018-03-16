using System.Threading;
using System.Threading.Tasks;

namespace SimMach {
    public interface IEnv {
        void Debug(string starting);
        CancellationToken Token { get; }
        Task Delay(int i, CancellationToken token = default (CancellationToken));
    }
}