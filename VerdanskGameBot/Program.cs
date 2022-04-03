using Discord;
using Discord.Net;
using Discord.WebSocket;
using Microsoft.Extensions.Configuration;
using NLog;
using NLog.Targets;
using System;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace VerdanskGameBot
{
    class Program
    {
        #region Main & Logging

        internal static Logger Log = LogManager.GetCurrentClassLogger();
        private bool IsVerbose = false;
        internal static CancellationTokenSource ExitCancel = new CancellationTokenSource();
        internal static bool IsExiting = false;

        private static void Main(string[] args)
        {
            Parallel.ForEach(LogManager.Configuration.AllTargets.Where(it => it.GetType() == typeof(FileTarget)), new ParallelOptions { CancellationToken = ExitCancel.Token }, target =>
           {
               var filepath = (target as FileTarget).FileName.Render(null);
               if (File.Exists(filepath))
               {
                   var createTime = File.GetCreationTime(filepath);
                   var splitpath = filepath.Split('.');
                   File.Move(filepath, $"{splitpath[0]}_{createTime:yyyy-MM-dd_HH-mm-ss}.{splitpath[1]}");
               }
           });

            new Program().MainApp();

            Console.CancelKeyPress += Console_SIGINT;
            AppDomain.CurrentDomain.ProcessExit += Process_SIGTERM;

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
                    if (IsVerbose)
                        Log.Info(logmsg);
                    break;
                case LogSeverity.Debug:
                    Log.Debug(logmsg);
                    break;
                default:
                    break;
            }

            return Task.CompletedTask;
        }
        #endregion

        internal static DiscordSocketClient BotClient;

        private void MainApp()
        {
            Log.Info("");
            Log.Info("=====[ Starting Verdansk GameBot ]=====");
            Log.Info("");

            var token = "";

            try
            {
                token = new ConfigurationBuilder()
#if DEBUG
                .AddUserSecrets<Program>().Build()["BotToken"];
#else
                .AddJsonFile("BotConfig.json").Build()["BotToken"];
#endif
            }
            catch (Exception ex)
            {
                Log.Trace(ex);
            }

            if (string.IsNullOrEmpty(token))
            {
                Log.Error("No discord application token specified. Please specify \"BotToken\": \"<DISCORD_APP_TOKEN>\" in \"BotConfig.json\" in the same folder as executable.");
                Environment.Exit(-1);
                return;
            }

            BotClient = new DiscordSocketClient(new DiscordSocketConfig()
            {
                LogLevel = IsVerbose ? LogSeverity.Debug : LogSeverity.Info
            });

            BotClient.Log += ClientLog;
            BotClient.Ready += OnBotReady;

            BotClient.SetActivityAsync(new Game("\r\nRefugees", ActivityType.Watching)).Wait();
            BotClient.LoginAsync(TokenType.Bot, token).Wait();
            BotClient.StartAsync().Wait();
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
