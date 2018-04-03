# sim-cluster

This is my current work-in-progress on running event-driven
distributed systems inside [discrete event simulation](https://en.wikipedia.org/wiki/Discrete_event_simulation).

## Try it out

This is a .NET Core 2.0. You should be able to open it in a IDE
(e.g. in JetBrains Rider) and run `Runtime/SimMach.csproj` project.

The output should be something like this:

![screenshot.png](screenshot.png)


Alternatively, you could try launching everything from the CLI with
something like:

```bash
$ dotnet run --project Runtime
```

## Details

`Sim-cluster` builds up on the previous work:

1. [SimCPU](https://github.com/abdullin/simcpu) - simulate CPU job
   scheduler (easier than it sounds);
2. [SimRing](https://gist.github.com/abdullin/af7c9b7fd4aa58cadcc346c8e194d9ab) -
   simulate ring benchmark;
3. [SimAsync](https://github.com/abdullin/simasync) - plug into .NET
   Core async/await to simulate processes running in parallel;
4. [SimCluster](https://github.com/abdullin/sim-cluster) - this.

This project introduces:

* **Simplified simulation of TCP/IP**. This includes connection
  handshake, SEQ/ACK numbers and reorder buffers. There is now proper
  shutdown sequence and no packet re-transmissions.
* **Durable node storage** in form of per-machine folders used by the
  LMDB database.
* **Configurable system topology** - machines, services and network
  connections.
* **Simulation plans** that specify how we want to run the simulated
  topology. This includes a graceful chaos monkey.
* **Simulating power outages** by erasing future for the affected
  systems.
* **Network profiles** - ability to configure latency, packet loss
  ratio and logging per network connection.

## Dive in

To dive in take a look at the `Program.cs`. It generates a simulation
scenario that is then executed.

A scenario could look like this:

```csharp
public static ScenarioDef InventoryMoverBotOver3GConnection() {
    var test = new ScenarioDef();
    // define network connections and provide network profiles for them
    test.Connect("botnet", "public", NetworkProfile.Mobile3G);
    test.Connect("public", "internal", NetworkProfile.AzureIntranet);
    // install services on the machines
    test.AddService("cl.internal", InstallCommitLog);
    test.AddService("api1.public", InstallBackend("cl.internal"));
    test.AddService("api2.public", InstallBackend("cl.internal"));
    // configure a bot that will create workload and verify results 
    var mover = new InventoryMoverBot {
        Servers = new []{"api1.public", "api2.public"},
        RingSize = 7,
        Iterations = 30,
        Delay = 4.Sec(),
        HaltOnCompletion = true
    };
    
    test.AddBot(mover);
    
    // define a plan for the simulation (who will control the machines)
    // this is optional, but a chaos monkey is cute...
    var monkey = new GracefulChaosMonkey {
        ApplyToMachines = s => s.StartsWith("api"),
        DelayBetweenStrikes = r => r.Next(5,10).Sec()
    };
    test.Plan = monkey.Run;
    return test;
}
```

Installer functions bring together the necessary dependencies and
return an instance of `IEngine`:

```csharp
static Func<IEnv, IEngine> InstallBackend(string cl) {
    return env => {
        var client = new CommitLogClient(env, cl + ":443");
        return new BackendServer(env, 443, client);
    };
}
static IEngine InstallCommitLog(IEnv env) {
    return new CommitLogServer(env, 443);
}
```

`BackendServer` is a simplistic event-driven server that has its own
projection thread and a (command) request handler. It commits data to
the `CommitLog` from which other server instances could get the same
data.

In theory, the same business logic should be able to run in the real
world environment as well. I didn't get to that part, yet.


# Licenses

This project is licensed under MIT license and uses:

* Portions of code from
  [FDB .NET Client](https://github.com/Doxense/foundationdb-dotnet-client)
  under 3-clause BSD to store data in LMDB;
* Random function from
  [GeneticSharp](https://github.com/giacomelli/GeneticSharp) under MIT.




