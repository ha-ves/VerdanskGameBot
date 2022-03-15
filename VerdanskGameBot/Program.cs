using Discord;
using Discord.Net;
using Discord.WebSocket;
using NLog;
using NLog.Targets;
using System;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading.Tasks;

namespace VerdanskGameBot
{
    class Program
    {
        #region Main & Logging
        static internal Program Current;
        static Task Main(string[] args)
        {
            Parallel.ForEach(LogManager.Configuration.AllTargets.Where(it => it.GetType() == typeof(FileTarget)), target =>
            {
                var filepath = (target as FileTarget).FileName.Render(null);
                if (File.Exists(filepath))
                {
                    var createTime = File.GetCreationTime(filepath);
                    var splitpath = filepath.Split('.');
                    File.Move(filepath, $"{splitpath[0]}_{createTime.ToString("yyyy-MM-dd_HH-mm-ss")}.{splitpath[1]}");
                }
            });

            Current = new Program();
            Current.MainApp().Wait();

            while (true)
            {
                var cmd = Console.ReadLine();
                if (cmd == "exit")
                {
                    Log.Info("=== Verdansk GameBot Stop initialized ===");
                    Current.BotClient.StopAsync().Wait();
                    break;
                }
            }

            Log.Info("");
            Log.Info("=====[ Verdansk GameBot Stopped ]=====");
            Log.Info("");
            return Task.CompletedTask;
        }

        static internal readonly Logger Log = LogManager.GetCurrentClassLogger();
        bool IsVerbose = false;
        Task ClientLog(LogMessage msg)
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

        DiscordSocketClient BotClient;
        internal CommandService CmdSvc;
        GameServerWatcher Watcher;

        async Task MainApp()
        {
            Log.Info("");
            Log.Info("=====[ Starting Verdansk GameBot ]=====");
            Log.Info("");

            BotClient = new DiscordSocketClient(new DiscordSocketConfig()
            {
                LogLevel = IsVerbose ? LogSeverity.Debug : LogSeverity.Info
            });

            BotClient.Log += ClientLog;
            BotClient.Ready += OnBotReady;

            await BotClient.SetActivityAsync(new Game("\r\nRefugees", ActivityType.Watching));

            var token = Environment.GetEnvironmentVariable("BotToken");

            await BotClient.LoginAsync(TokenType.Bot, token);
            await BotClient.StartAsync();
        }

        async Task OnBotReady()
        {
            CmdSvc = new CommandService(BotClient);
            Watcher = new GameServerWatcher();

            Log.Info("");
            Log.Info("=====[ Verdansk GameBot Started ]=====");
            Log.Info("");


        }
    }
}
