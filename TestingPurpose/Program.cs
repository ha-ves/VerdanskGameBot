using RconSharp;
using System;
using System.Diagnostics;
using System.Threading;

namespace TestingPurpose
{
    class Program
    {
        static async System.Threading.Tasks.Task Main(string[] args)
        {
            var rcon = RconClient.Create("localhost", 27015);
            await rcon.ConnectAsync();
            var cmd_resp = await rcon.ExecuteCommandAsync("test");

            Debugger.Break();
        }
    }
}
