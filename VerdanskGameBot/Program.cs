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
using Npgsql;
using System;
using System.Collections;
using System.Collections.Generic;
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
        #region Internal

        internal static Logger Log { get; } = LogManager.GetCurrentClassLogger();
        internal static CancellationTokenSource ExitCancel { get; } = new CancellationTokenSource();
        internal static bool IsExiting { get; private set; } = false;
        internal static string Version { get; private set; } = "";

        #endregion

        #region Bot Info

        internal static IConfigurationRoot BotConfig { get; private set; } = null;
        internal static DiscordSocketClient BotClient { get; private set; }
        internal static IPAddress LAN_IP { get; private set; }
        internal static bool IsConnected { get => BotClient.ConnectionState == ConnectionState.Connected; }

        #endregion

        #region Entry point main()

        private static void Main(string[] args)
        {
#if DEBUG
            Console.WriteLine("Waiting for debugger");
            while (!Debugger.IsAttached) ;
            Console.WriteLine("Debugger attached");
#endif
            LogManager.Configuration = new XmlLoggingConfiguration(
                    XmlReader.Create(GetRes("NLog.config"))
                );

            #region CmdOptions

            var regArgs = new[]
            {
                "--help",
                "--version",
                "--trace",
                "--service"
            };

            if (args.Contains("--version"))
            {
                var assembly = Assembly.GetEntryAssembly();
                var info = assembly.GetCustomAttribute<AssemblyInformationalVersionAttribute>().InformationalVersion.Split('+');
                Version = 'v' + info.FirstOrDefault();
                var buildtime = DateTime.ParseExact(info.Last(), "yyyyMMddHHmmss", null);

                var verStr = $"{assembly.GetCustomAttribute<AssemblyProductAttribute>().Product} {Version}" + Environment.NewLine
                    + $"{assembly.GetCustomAttribute<AssemblyCopyrightAttribute>().Copyright}" + Environment.NewLine
                    + $"Built ({buildtime:F})." + Environment.NewLine;

                Console.WriteLine(verStr);
            }

            if (args.Contains("--help") || !args.Any(a => regArgs.Contains(a)))
            {
                if (args.Any(a => !regArgs.Contains(a)))
                    Console.WriteLine($"Invalid Args : '{args.Where(a => !regArgs.Contains(a)).Aggregate((a, b) => a + ' ' + b)}'" + Environment.NewLine);

                var helpStr = $"Usage : {Assembly.GetEntryAssembly().GetName().Name} [Args]" + Environment.NewLine
                    + Environment.NewLine
                    + "Args :" + Environment.NewLine
                    + "    --help     : Prints this help screen" + Environment.NewLine
                    + "    --version  : Prints version" + Environment.NewLine
                    + "    --trace    : Enable most verbose/indepth logging" + Environment.NewLine
                    + "    --service  : Run as a system service" + Environment.NewLine
                    + Environment.NewLine
                    + "Args also available via BotConfig.json file next to executable";

                Console.WriteLine(helpStr);

                Environment.Exit(-(int)ExitCodes.CmdArgsInvalid);
            }

            if (args.Contains("--trace"))
            {
                LogManager.Configuration.FindRuleByName("consolelog").SetLoggingLevels(LogLevel.Trace, LogLevel.Fatal);
                LogManager.Configuration.FindRuleByName("debuglog").EnableLoggingForLevel(LogLevel.Trace);

                LogManager.ReconfigExistingLoggers();
            }

            #endregion

            Parallel.ForEach(LogManager.Configuration.AllTargets.Where(it => it.GetType() == typeof(FileTarget)),
                parallelOptions: new ParallelOptions { CancellationToken = ExitCancel.Token }, target =>
                {
                    var filepath = (target as FileTarget).FileName.Render(null);
                    if (File.Exists(filepath))
                    {
                        var createTime = File.GetLastAccessTime(filepath);
                        var splitpath = filepath.Split('.');
                        File.Move(filepath, $"{splitpath[0]}_{createTime:yyyy-MM-dd_HH-mm-ss}.{splitpath[1]}");
                        return;
                    }
                });

            new Program().MainApp(args);

            Console.CancelKeyPress += Console_SIGINT;
            AppDomain.CurrentDomain.ProcessExit += Process_SIGTERM;

            if (args.Contains("--service"))
            {
                Log.Debug("App IS NOT Console Interactive (ran as a system service)");
                new ManualResetEvent(false).WaitOne();
            }
            else
            {
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
            if (!IsExiting)
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

        private void MainApp(string[] args)
        {
            #region Pre-Start Debugging

#if DEBUG
            
#endif

            #endregion

            Log.Info("");
            Log.Info("=====[ Starting Verdansk GameBot ]=====");
            Log.Info($"               {Version}");
            Log.Info("");

            var token = "";
            var isverbose = false;

            #region Loading Configuration

            Log.Trace("Loading Configuration...");
            try
            {
                if (!File.Exists("BotConfig.json"))
                {
                    Log.Trace("No BotConfig.json file found. Creating one...");
                    var file = File.Create("BotConfig.json");
                    GetRes("BotConfig.json").CopyToAsync(file).Wait();
                    file.Close();
                    Log.Trace("Created \"BotConfig.json\" file with default values.");
                }
                else
                {
                    Log.Trace("BotCOnfig.json exists");
                }

                BotConfig = new ConfigurationBuilder()
                    .AddJsonFile("BotConfig.json")
                    .Build();
                
                token = BotConfig["BotToken"];
                Log.Trace("BotToken Available.");
                isverbose = bool.Parse(BotConfig["Verbose"]);
                Log.Trace("Verbose Available.");
                LAN_IP = IPAddress.Parse(BotConfig["LocalIP"]);
                Log.Trace("LocalIP Available.");

                if (isverbose)
                    LogManager.Configuration.FindRuleByName("consolelog").EnableLoggingForLevel(LogLevel.Debug);
            }
            catch (Exception ex)
            {
                Log.Fatal(ex, "Configuration parameters or file is not valid. (Please delete \"BotConfig.json\" if you want to reset.)");
                Environment.Exit(-(int)ExitCodes.BotConfigInvalid);
                return;
            }
            Log.Trace("Loaded Configuration.");

            #endregion

            #region Configuring NodeJS

            Log.Trace("Configuring NodeJS ...");
            try
            {
                var nodejs = StaticNodeJSService.InvokeFromStringAsync<string>(@"module.exports = (callback) => callback(null, process.versions);").Result;
                Log.Debug("Using NodeJS version " + JsonDocument.Parse(nodejs).RootElement.GetProperty("node"));
            }
            catch (Exception exc)
            {
                Log.Fatal(exc, "Can not start because NodeJS is not available. Please get from official release. (https://nodejs.org/en/download/current/)");

                Environment.Exit(-(int)ExitCodes.NodeJSNotAvail);
                return;
            }
            Log.Debug("Configured NodeJS.");

            #endregion

            #region Configuring gamedig

            Log.Trace("Configuring gamedig ...");
            try 
            {
                var gamedigver = StaticNodeJSService.InvokeFromStringAsync<string>(@"module.exports = (callback) => { callback(null, require('gamedig/package.json').version); }").Result;
                Log.Debug("Using node-gamedig version " + gamedigver);
            }
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
            Log.Debug("Configured gamedig.");

            #endregion

            #region Configuring Discord Bot

            Log.Trace("Configuring Discord Bot ...");
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

            Log.Debug("Configured Discord Bot.");

            #endregion

            #region Configuring Database

            Log.Trace("Configuring Database ...");
            try
            {
                Log.Trace($"Using {Enum.GetName(Enum.Parse<DbProviders>(BotConfig["DbProvider"]))} database");

                using (var db = new GameServerContextFactory().CreateDbContext(new[] { "--ConnStr", BotConfig["ConnectionString"], "--DbType", BotConfig["DbProvider"] }))
                {
                    Log.Trace("[DB] Current Migration : " +
                        $"{db.Database.GetAppliedMigrations().LastOrDefault()}");
                    var migrations = db.Database.GetPendingMigrations();
                    if (migrations.Any())
                    {
                        var str = migrations.Aggregate((a, b) => $"{a} -> {b}");
                        Log.Trace($"[DB] Migration Pending : {str}");
                        db.Database.Migrate();
                    }
                }
            }
            catch (ArgumentNullException e)
            {
                Log.Fatal(e, "Database configuration failed. Please check and provide valid database configuration in config file.");
                Environment.Exit(-(int)ExitCodes.DbConfigInvalid);
                return;
            }
            Log.Debug("Configured Database.");

            #endregion

            #region Pre-Bot Debugging

#if DEBUG

#endif

            #endregion

            #region Starting Discord Bot

            Log.Trace("Starting Discord Bot ...");

            BotClient.Log += ClientLog;
            BotClient.LoggedIn += BotClient.StartAsync;
            BotClient.Ready += OnBotReady;
            BotClient.Disconnected += OnBotDisconnect;

            BotClient.SetActivityAsync(new Game("Refugees", ActivityType.Watching)).Wait();
            BotClient.LoginAsync(TokenType.Bot, token).Wait();

            new EventWaitHandle(false, EventResetMode.ManualReset).WaitOne(5000);
            if (BotClient.ConnectionState != ConnectionState.Connected)
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

            #endregion
        }

        private async Task OnBotReady()
        {
            Log.Info("");
            Log.Info("=====[ Verdansk GameBot Started ]=====");
            Log.Info("");

            #region  OnBotReady Debugging

#if DEBUG

#endif

            #endregion

            await Command.CommandService.StartService(BotClient, ExitCancel.Token);
            GameServerWatcher.StartWatcher();
            }

        #region Misc

        private Task OnBotDisconnect(Exception arg)
        {
            return Task.CompletedTask;
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

        #endregion
    }
}
