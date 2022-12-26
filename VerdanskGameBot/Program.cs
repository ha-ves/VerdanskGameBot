using Discord;
using Discord.Net;
using Discord.WebSocket;
using Jering.Javascript.NodeJS;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage.ValueConversion;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Configuration.CommandLine;
using Microsoft.Extensions.Configuration.Json;
using Newtonsoft.Json.Converters;
using Newtonsoft.Json.Linq;
using NLog;
using NLog.Config;
using NLog.Fluent;
using NLog.Targets;
using System;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Net;
using System.Reflection;
using System.Security;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using System.Xml;
using VerdanskGameBot.Command;
using VerdanskGameBot.Ext;
using VerdanskGameBot.GameServer;
using VerdanskGameBot.GameServer.Db;

namespace VerdanskGameBot
{
    class Program
    {
        internal static IConfigurationRoot BotConfig = null;

        internal static Logger Log = LogManager.GetCurrentClassLogger();

        internal static DiscordSocketClient BotClient;

        internal static CancellationTokenSource ExitCancel = new CancellationTokenSource();

        internal static bool IsExiting = false, IsConnected = false;

        internal static IPAddress LocalIP;


        #region Main & Logging

        private static void Main(string[] args)
        {
#if DEBUG
            Log.Trace("Waiting for debugger");
            while (!Debugger.IsAttached) ;
            Log.Trace("Debugger attached");
#endif
            BotConfig = new ConfigurationBuilder().AddCommandLine(args).Build();

            LogManager.Configuration = new XmlLoggingConfiguration(
                XmlReader.Create(GetRes("NLog.config"))
            );

            if (BotConfig["trace"] != null)
                LogManager.Configuration.FindRuleByName("debuglog").EnableLoggingForLevel(LogLevel.Trace);

            Parallel.ForEach(LogManager.Configuration.AllTargets.Where(it => it.GetType() == typeof(FileTarget)),
                parallelOptions: new ParallelOptions { CancellationToken = ExitCancel.Token }, target =>
                {
                    var filepath = (target as FileTarget).FileName.Render(null);
                    if (File.Exists(filepath))
                    {
                        var createTime = File.GetLastAccessTime(filepath);
                        var splitpath = filepath.Split('.');
                        File.Move(filepath, $"{splitpath[0]}_{createTime:yyyy-MM-dd_HH-mm-ss}.{splitpath[1]}");
                    }
                });

            #region Pre-Start Debugging

#if DEBUG

            

#endif

            #endregion

            new Program().MainApp(args);

            Console.CancelKeyPress += Console_SIGINT;
            AppDomain.CurrentDomain.ProcessExit += Process_SIGTERM;

            if (args.Length > 0 && args[0] == "service")
            {
                Log.Debug("App IS NOT Console Interactive (system service)");
                new ManualResetEvent(false).WaitOne();
            }
            else {
                Log.Debug("App IS Console Interactive");
                while (true)
                {
                    var cmd = Console.ReadLine();
                    if (cmd == "exit")
                    {
                        Console_SIGINT(null, null);
                        break;
                    }
                }
            }
        }

        public static Stream GetRes(string resName)
        {
            return Assembly.GetEntryAssembly().GetManifestResourceStream(typeof(Program), resName);
        }

        private static void Process_SIGTERM(object sender, EventArgs e)
        {
            if(!IsExiting)
                ExitRequested();
        }

        private static void Console_SIGINT(object sender, ConsoleCancelEventArgs e)
        {
            ExitRequested();

            Environment.Exit((int)ExitCodes.OK);
        }

        private static void ExitRequested()
        {
            Log.Info("=== Verdansk GameBot Stop initialized ===");

            IsExiting = true;

            ExitCancel.Cancel();
            ExitCancel.Dispose();

            GameServerWatcher.Dispose();

            if (BotClient != null)
            {
                BotClient.StopAsync().Wait();
                BotClient.DisposeAsync().AsTask().Wait();
            }

            Task.Delay(100).Wait();

            Log.Info("");
            Log.Info("=====[ Verdansk GameBot Stopped ]=====");
            Log.Info("");

            NLog.LogManager.Shutdown();
        }

        #endregion

        private async void MainApp(string[] args)
        {
            Log.Info("");
            Log.Info("=====[ Starting Verdansk GameBot ]=====");
            Log.Info("");

            var token = "";
            var isverbose = false;

            try
            {
                if (!File.Exists("BotConfig.json"))
                {
                    Log.Trace("No BotConfig.json file found. Creating one...");
                    var file = File.Create("BotConfig.json");
                    Program.GetRes("BotConfig.json").CopyToAsync(file).Wait();
                    file.Close();
                    Log.Trace("Created \"BotConfig.json\" file with default values.");
                }

                BotConfig = new ConfigurationBuilder()
                    .AddJsonFile("BotConfig.json")
                    .AddCommandLine(args)
                    .Build();
                
                token = BotConfig["BotToken"];
                isverbose = bool.Parse(BotConfig["Verbose"]);
                LocalIP = IPAddress.Parse(BotConfig["LocalIP"]);

                if (isverbose)
                    LogManager.Configuration.FindRuleByName("consolelog").EnableLoggingForLevel(LogLevel.Debug);
            }
            catch (Exception ex)
            {
                Log.Fatal(ex, "Configuration parameters or file is not valid. (Please delete \"BotConfig.json\" if you want to reset.)");
                Environment.Exit(-(int)ExitCodes.BotConfigInvalid);
                return;
            }

            try
            {
                StaticNodeJSService.InvokeFromStringAsync<string>(@"module.exports = (callback) => callback(null, 'NODEJSTEST');").Wait();
            }
            catch (Exception exc)
            {
                Log.Fatal(exc, "Can not start because NodeJS is not available. Please get from official release. (https://nodejs.org/en/download/current/)");

                Environment.Exit(-(int)ExitCodes.NodeJSNotAvail);
                return;
            }

            CheckGamedigInstall();

            BotClient = new DiscordSocketClient(new DiscordSocketConfig()
            {
#if DEBUG
                LogLevel = LogSeverity.Debug,
#else
                LogLevel = isverbose ? LogSeverity.Verbose : LogSeverity.Info,
#endif
                DefaultRetryMode = RetryMode.RetryTimeouts,
                GatewayIntents = GatewayIntents.AllUnprivileged & ~(GatewayIntents.GuildInvites | GatewayIntents.GuildScheduledEvents)
            });

            await DatabaseInit();

            BotClient.Log += ClientLog;
            BotClient.LoggedIn += BotClient.StartAsync;
            BotClient.Ready += OnBotReady;
            BotClient.Disconnected += BotClient_Disconnected;

            await BotClient.SetActivityAsync(new Game("Refugees", ActivityType.Watching));
            await BotClient.LoginAsync(TokenType.Bot, token);

            new EventWaitHandle(false, EventResetMode.ManualReset).WaitOne(5000);
            if (BotClient.Rest.LoginState != LoginState.LoggedIn)
            {
                if (string.IsNullOrEmpty(token))
                {
                    Log.Fatal($"No discord application token specified. Please specify \"BotToken\": \"<DISCORD_APP_TOKEN>\" in config file.");
                    Environment.Exit(-(int)ExitCodes.BotTokenInvalid);
                    return;
                }
                else
                {
                    Log.Fatal("Failed to login to discord. Something undetectable is wrong.");
                    Environment.Exit(-(int)ExitCodes.BotLoginFailed);
                    return;
                }
            }
        }

        private Task BotClient_Disconnected(Exception arg)
        {
            Debugger.Break();

            return Task.CompletedTask;
        }

        private void CheckGamedigInstall()
        {
            try { StaticNodeJSService.InvokeFromStringAsync<string>(@"module.exports = (callback) => { require('gamedig'); callback(null, 'GAMEDIGTEST'); }").Wait(); }
            catch
            {
                Log.Info("{ gamedig } not available, trying to install...");

                var npmproc = new Process();
                try
                {
                    npmproc.StartInfo = new ProcessStartInfo
                    {
                        FileName = "npm",
                        Arguments = "install gamedig",
                        UseShellExecute = true,
                    };
                    npmproc.Start();
                    npmproc.WaitForExit();
                }
                catch (Exception ex)
                {
                    Log.Fatal(ex, "Failed to install { gamedig }. Can not start because { gamedig } is not available. Try running 'npm install gamedig' manually." + (npmproc.ExitCode == 127 ? " (npm NOT FOUND)" : ""));
                    Environment.Exit(-(int)ExitCodes.GamedigMissing);
                    return;
                }
            }
        }

        private async Task DatabaseInit()
        {
            try
            {
                Log.Debug($"Configurating Database... Using {Enum.GetName(Enum.Parse<DbProviders>(BotConfig["DbProvider"]))} database");

                using (var db = GameServerDb.GetContext())
                {
                    Log.Trace("[DB] Current Migration : " +
                        $"{(await db.Database.GetAppliedMigrationsAsync(ExitCancel.Token)).LastOrDefault()}");
                    var migrations = await db.Database.GetPendingMigrationsAsync(ExitCancel.Token);
                    if (migrations.Any())
                    {
                        var str = migrations.Aggregate((a, b) => $"{a} -> {b}");
                        Log.Trace($"[DB] Migration Pending : {str}");
                        await db.Database.MigrateAsync(ExitCancel.Token);
                    }
                }
            }
            catch (ArgumentNullException e)
            {
                Log.Fatal(e, "Database configuration failed. Please check and provide valid database configuration in config file.");
                Environment.Exit(-(int)ExitCodes.DbConfigInvalid);
                return;
            }
        }

        private async Task OnBotReady()
        {
            Log.Info("");
            Log.Info("=====[ Verdansk GameBot Started ]=====");
            Log.Info("");
#if DEBUG
            using (var it = GameServerDb.GetContext())
            {
                it.Add(new GameServerModel()
                {
                    GameType = "przomboid",
                    ServerName = "testarea",
                    LastOnline = DateTimeOffset.MinValue,
                    AddedBy = 1,
                    ChannelId = 1,
                    MessageId = 1,
                    IP = IPAddress.IPv6Loopback,
                    GamePort = 12727,
                    AddedSince = DateTimeOffset.Now,
                });
                await it.SaveChangesAsync();
            }

            Debugger.Break();
#endif
            await CommandService.StartService(BotClient, ExitCancel.Token);
            GameServerWatcher.StartWatcher();
        }

        private Task ClientLog(LogMessage msg)
        {
            switch (msg.Severity)
            {
                case LogSeverity.Critical:
                    Log.Fatal(msg.Exception, $"{msg.Source} // {msg.Message}");
                    break;
                case LogSeverity.Error:
                    Log.Error(msg.Exception, $"{msg.Source} // {msg.Message}");
                    break;
                case LogSeverity.Warning:
                    Log.Warn(msg.Exception, $"{msg.Source} // {msg.Message}");
                    break;
                case LogSeverity.Info:
                    Log.Info($"{msg.Source} // {msg.Message}");
                    break;
                case LogSeverity.Verbose:
                    Log.Debug($"{msg.Source} // {msg.Message}");
                    break;
                case LogSeverity.Debug:
                    Log.Trace($"{msg.Source} // {msg.Message}");
                    break;
                default:
                    break;
            }

            return Task.CompletedTask;
        }
    }
}
