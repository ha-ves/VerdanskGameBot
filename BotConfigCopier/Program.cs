using Microsoft.Extensions.Configuration;
using System;

namespace BotConfigCopier
{
    internal class Program
    {
        static void Main(string[] args)
        {
            new ConfigurationBuilder().AddUserSecrets<VerdanskGameBot.AppSecrets>();
        }
    }
}
