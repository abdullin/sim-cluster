using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;

namespace SimMach.Sim {
    public class SimMachine {
        public readonly string Name;
        public readonly SimRuntime Runtime;
        public readonly SimCluster Cluster;

        
        // ooh, clock drift !
        public TimeSpan Time => Runtime.Time;

        public readonly Dictionary<string, SimService> Services = new Dictionary<string, SimService>();
        readonly Dictionary<ushort, SimSocket> _sockets = new Dictionary<ushort, SimSocket>();

        public SimMachine(string name, SimRuntime runtime, SimCluster cluster) {
            Name = name;
            Runtime = runtime;
            Cluster = cluster;
        }

        public void Install(ServiceId id, Func<IEnv, IEngine> service) {
            Services.Add(id.Service, new SimService(this, id, service));
        }

        int _procId;

        public int NextProcID() {
            return _procId++;
        }

        public ushort NextSocketID() {
            for (ushort i = 10000; i < ushort.MaxValue; i++) {
                if (!_sockets.ContainsKey(i)) {
                    return i;
                }
            }
            throw new IOException("No free sockets");
        }

        public void ReleaseSocket(ushort port) {
            _sockets.Remove(port);
        }


        public void Debug(string message) {
            Runtime.Debug($"  {message}");
        }

        public async Task<IConn> Connect(SimProc process, SimEndpoint destination) {
            SimRoute route;
            if (!Cluster.TryGetRoute(Name, destination.Machine, out route)) {
                throw new IOException($"Route not found");
            }
            
            
            // todo: allocate port
            // todo: allow Azure SNAT delay scenario
            var socketId = NextSocketID();
            var source = new SimEndpoint(Name, socketId);
            
            

            var clientSocket = new SimSocket(process, source, Cluster);
            _sockets.Add(socketId, clientSocket);

            
            var conn = new SimConn(clientSocket, destination, process);
            
            clientSocket._connections.Add(destination, conn);
            
            
            // handshake
            await conn.Write(null, SimFlag.Syn);
            
            var response = await conn.Read(5.Sec());
            if (response.Flag != (SimFlag.Ack | SimFlag.Syn)) {
                await conn.Write(null, SimFlag.Reset);
                clientSocket._connections.Remove(destination);
                throw new IOException("Failed to connect");

            }
            await conn.Write(null, SimFlag.Ack);
            
            return new ClientConn(conn);
        }
        
        public async Task<ISocket> Bind(SimProc proc, ushort port, TimeSpan timeout) {
            // socket is bound to the owner
            var endpoint = new SimEndpoint(proc.Id.Machine, port);

            if (_sockets.ContainsKey(port)) {
                throw new IOException($"Address {endpoint} in use");
            }

            var socket = new SimSocket(proc, endpoint, Cluster);
            _sockets.Add(port, socket);

            proc.RegisterSocket(socket);

            return socket;
        }

        public bool TryDeliver(SimPacket msg) {
            if (_sockets.TryGetValue(msg.Destination.Port, out var socket)) {
                socket.Deliver(msg);
                return true;
            }

            return false;
        }
        
        

        public string GetServiceFolder(string service, string name) {
            var sim = Runtime.GetSimulationFolder();

            var folder = Path.Combine(Name, $"{service}_{name}");
            var localPath = Path.Combine(sim, folder);
            if (!Directory.Exists(localPath)) {
                Debug($"Allocating folder {folder}");
                Directory.CreateDirectory(localPath);
            } else {
                Debug($"Reusing folder {folder}");
            }
            

            return localPath;
        }

        public void WipeStorage() {
            var sim = Runtime.GetSimulationFolder();

            var machinePath = Path.Combine(sim, Name);
            if (Directory.Exists(machinePath)) {
                Debug($"ERASE STORAGE for machine {Name}");
                Directory.Delete(machinePath, true);
            }
          
        }
    }
}