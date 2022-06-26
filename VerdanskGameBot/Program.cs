using Discord;
using Discord.Net;
using Discord.WebSocket;
using Jering.Javascript.NodeJS;
using Microsoft.Extensions.Configuration;
using NLog;
using NLog.Config;
using NLog.Targets;
using NodaTime;
using System;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Net;
using System.Reflection;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using System.Xml;

namespace VerdanskGameBot
{
    class Program
    {
        #region Main & Logging

        internal static Logger Log = LogManager.GetCurrentClassLogger();

        internal static DiscordSocketClient BotClient;

        internal static CancellationTokenSource ExitCancel = new CancellationTokenSource();

        internal static bool IsExiting = false, IsConnected = false;

        internal static IPAddress LocalIP;

        private static void Main(string[] args)
        {
#if DEBUG
            Console.WriteLine("Waiting for debugger");
            while (!Debugger.IsAttached) ;
            Console.WriteLine("Debugger attached");
#endif
            LogManager.Configuration = new XmlLoggingConfiguration(
                XmlReader.Create(Assembly.GetExecutingAssembly().GetManifestResourceStream(typeof(Program), "NLog.config"))
            );

            Parallel.ForEach(LogManager.Configuration.AllTargets.Where(it => it.GetType() == typeof(FileTarget)), new ParallelOptions { CancellationToken = ExitCancel.Token }, target =>
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


            new Program().MainApp();

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

        private static void Process_SIGTERM(object sender, EventArgs e)
        {
            if(!IsExiting)
                ExitRequested();
        }

        private static void Console_SIGINT(object sender, ConsoleCancelEventArgs e)
        {
            ExitRequested();

            Environment.Exit(0);
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

        private Task ClientLog(LogMessage msg)
        {
            var logmsg = msg.ToString(prependTimestamp: false);
            switch (msg.Severity)
            {
                case LogSeverity.Critical:
                    Log.Fatal(logmsg);
                    break;
                case LogSeverity.Error:
                    Log.Error(logmsg);
                    break;
                case LogSeverity.Warning:
                    Log.Warn(logmsg);
                    break;
                case LogSeverity.Info:
                    Log.Info(logmsg);
                    break;
                case LogSeverity.Verbose:
                    Log.Debug(logmsg);
                    break;
                case LogSeverity.Debug:
                    Log.Trace(logmsg);
                    break;
                default:
                    break;
            }

            return Task.CompletedTask;
        }
        #endregion

        private void MainApp()
        {
            Log.Info("");
            Log.Info("=====[ Starting Verdansk GameBot ]=====");
            Log.Info("");

            try
            {
                StaticNodeJSService.InvokeFromStringAsync<string>(@"module.exports = (callback) => callback(null, 'NODEJSTEST');").Wait();
            }
            catch (Exception exc)
            {
                Log.Fatal(exc, "Can not start because NodeJS is not available. Please get from official release. (https://nodejs.org/en/download/current/)");
                
                Environment.Exit(-500);
                return;
            }

            CheckGamedigInstall();

            var token = "";
            var isverbose = false;

            try
            {
                if (!File.Exists("BotConfig.json"))
                {
                    Log.Debug("No BotConfig.json file found. Creating one...");
                    var file = File.Create("BotConfig.json");
                    Assembly.GetExecutingAssembly().GetManifestResourceStream(this.GetType(), "BotConfig.json").CopyToAsync(file).Wait();
                    file.Close();
                    Log.Debug("Created \"BotConfig.json\" file with default values.");
                }

                var config = new ConfigurationBuilder().AddJsonFile("BotConfig.json").Build();

                token = config["BotToken"];
                isverbose = bool.Parse(config["Verbose"]);
                LocalIP = IPAddress.Parse(config["LocalIP"]);
            }
            catch (Exception ex)
            {
                Log.Fatal(ex, "Configuration File is not valid. Please delete \"BotConfig.json\" to reset.");
                Environment.Exit(-500);
                return;
            }

            if (string.IsNullOrEmpty(token))
            {
                Log.Fatal("No discord application token specified. Please specify \"BotToken\": \"<DISCORD_APP_TOKEN>\" in \"BotConfig.json\" in the same folder as executable.");
                Environment.Exit(-500);
                return;
            }

            BotClient = new DiscordSocketClient(new DiscordSocketConfig()
            {
                LogLevel = isverbose ? LogSeverity.Debug : LogSeverity.Info,
                DefaultRetryMode = RetryMode.RetryTimeouts,
                GatewayIntents = GatewayIntents.AllUnprivileged & ~(GatewayIntents.GuildInvites | GatewayIntents.GuildScheduledEvents)
            });

            BotClient.Log += ClientLog;
            BotClient.Ready += OnBotReady;

            BotClient.SetActivityAsync(new Game("Refugees", ActivityType.Watching)).Wait();
            BotClient.LoginAsync(TokenType.Bot, token).Wait();
            BotClient.StartAsync().Wait();
        }

        private void CheckGamedigInstall()
        {
            try { StaticNodeJSService.InvokeFromStringAsync<string>(@"module.exports = (callback) => { require('gamedig'); callback(null, 'GAMEDIGTEST'); }").Wait(); }
            catch
            {
                Log.Debug("{ gamedig } not available, trying to install...");

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

                    Debugger.Break();

                    Environment.Exit(-500);
                    return;
                }
            }
        }

        private Task OnBotReady()
        {
            CommandService.StartService(BotClient);
            GameServerWatcher.StartWatcher();

            Log.Info("");
            Log.Info("=====[ Verdansk GameBot Started ]=====");
            Log.Info("");

            return Task.CompletedTask;
        }
    }
}
