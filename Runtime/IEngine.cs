using System.Threading.Tasks;

namespace SimMach {
    public interface IEngine {
        Task Run();
        Task Dispose();
    }
}