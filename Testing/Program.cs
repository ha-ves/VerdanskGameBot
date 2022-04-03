using Jering.Javascript.NodeJS;
using System;
using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Threading.Tasks;

namespace Testing
{
    internal class Program
    {
        static async Task Main(string[] args)
        {
            var ret = await StaticNodeJSService.InvokeFromStringAsync<string>(@"
                module.exports = async () => {
                    return await require('gamedig').query({
                        type: 'przomboid',
                        host: 'tekat.my.id'
                    })
                }
                ");

            Debugger.Break();
        }
    }
}
