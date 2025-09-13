using NLog;
using NLog.Config;
using NLog.Targets;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using VerdanskGameBot.Ext;

namespace VerdanskGameBot
{
    public class Program
    {
        private static Logger? _logger;
        private static readonly CancellationTokenSource _intToken = new();
        private static readonly ManualResetEventSlim _exitRdy = new(false);

        private static bool IsExiting { get; set; } = false;
        private static LogFactory LogFactory { get; } = new();

        private static GameBotApp? App { get; set; } = null;

        private static void Main(string[] args)
        {
#if DEBUG
            Console.WriteLine("Waiting for debugger");
            while (!Debugger.IsAttached) ;
            Console.WriteLine("Debugger attached");
#endif
            Console.CancelKeyPress += Console_SIGINT!;
            AppDomain.CurrentDomain.ProcessExit += Process_SIGTERM!;

            #region CmdOptions

            var regArgs = new[]
            {
                "--help",
                "--version",
                "--verbose",
                "--service",
                "--one-guild"
            };

            var assembly = typeof(GameBotApp).Assembly;
            var info = assembly.GetCustomAttribute<AssemblyInformationalVersionAttribute>()!
                .InformationalVersion.Split('+');
            var ver = 'v' + info.FirstOrDefault();

            if (args.Contains("--version"))
            {
                var buildtime = DateTime.ParseExact(info.Last(), "yyyyMMddHHmmss", null);

                var verStr = $"{assembly.GetCustomAttribute<AssemblyProductAttribute>()!.Product} {ver}" + Environment.NewLine
                    + $"{assembly.GetCustomAttribute<AssemblyCopyrightAttribute>()!.Copyright}" + Environment.NewLine
                    + $"Built ({buildtime:F})." + Environment.NewLine;

                Console.WriteLine(verStr);
                ExitWithCode(ExitCode.CmdArgsInvalid);
            }

            if (args.Contains("--help"))
            {
                if (args.Any(a => !regArgs.Contains(a)))
                    Console.WriteLine($"Invalid Args : '{args.Where(a => !regArgs.Contains(a)).Aggregate((a, b) => a + ' ' + b)}'" + Environment.NewLine);

                var helpStr = $"Usage : {assembly.GetName().Name} [Args]" + Environment.NewLine
                    + Environment.NewLine
                    + "Args :" + Environment.NewLine
                    + "    --help     : Prints this help screen" + Environment.NewLine
                    + "    --version  : Prints version" + Environment.NewLine
                    + "    --verbose  : Enable verbose logging" + Environment.NewLine
                    + "    --service  : Run as a system service" + Environment.NewLine
                    + Environment.NewLine
                    + "Args also available via BotConfig.json file next to executable";

                Console.WriteLine(helpStr);
                ExitWithCode(ExitCode.CmdArgsInvalid);
            }

            _ = GameBotApp.TryGetRes("NLog.config", out var nlogconfigstream);
            var logconfig = new XmlLoggingConfiguration(new StreamReader(nlogconfigstream!));

            var isTrace = args.Contains("--verbose");
            if (isTrace)
            {
                var loglvl =
#if DEBUG
                    LogLevel.Trace;
#else
                    LogLevel.Debug;
#endif
                logconfig.FindRuleByName("consolelog")!.EnableLoggingForLevels(loglvl, LogLevel.Info);
                logconfig.FindRuleByName("debuglog")!.EnableLoggingForLevel(loglvl);

                Console.WriteLine($"[DDDD] Verbose logging enabled, log level set to '{loglvl.Name}'");
            }

            foreach (var filetarget in logconfig.AllTargets
                .Where(it => it.GetType() == typeof(FileTarget))
                .Cast<FileTarget>())
            {
                var filepath = filetarget.FileName.Render(LogEventInfo.CreateNullEvent());
                if (File.Exists(filepath))
                {
                    var createTime = File.GetLastWriteTime(filepath);
                    var dir = Path.GetDirectoryName(filepath) ?? string.Empty;
                    var filename = Path.GetFileNameWithoutExtension(filepath);
                    var ext = Path.GetExtension(filepath);
                    var rotfile = $"{filename}_{createTime:yyyy-MM-dd_HH-mm-ss}{ext}";
                    var rotfilepath = Path.Join(dir, rotfile);
                    File.Move(filepath, rotfilepath);

                    if (args.Contains("--verbose"))
                        Console.WriteLine($"[DDDD] Rotated old log file '{filepath}' to '{rotfilepath}'");
#if DEBUG
                    if (Debugger.IsAttached)
                    {
                        Console.WriteLine($"[[]<< Removing old log file: '{rotfilepath}' (attached to debugger). >>[]]");
                        File.Delete(rotfilepath);
                    }
#endif
                }
            }

            #endregion

            LogFactory.Configuration = logconfig;
            _logger = LogFactory.GetCurrentClassLogger();

            var app = new GameBotApp(LogFactory, isTrace, args);
            var apptask = Task.Run(() => app.StartAsync(_intToken.Token));

            apptask.ConfigureAwait(false).GetAwaiter()
                .OnCompleted(() => 
                {
                    apptask.Exception?.Handle(ex =>
                    {
                        if (ex.Data.Contains(nameof(ExitCode)))
                        {
                            if (ex.Data[nameof(ExitCode)] is var exitCode && exitCode is not null)
                            {
                                IsExiting = true;
                                ExitWithCode((ExitCode)exitCode);
                            }
                            else
                                Environment.Exit(-1);

                            return true;
                        }

                        return false;
                    });
                    _exitRdy.Set();
                });

            if (args.Contains("--service"))
            {
                _logger.Debug("App IS NOT Console Interactive (ran as a system service)");
                new ManualResetEvent(false).WaitOne();
            }
            else
            {
                _logger.Debug("App IS Console Interactive");
                while (true)
                {
                    var cmd = Console.ReadLine();
                    if (cmd == "exit")
                    {
                        Console_SIGINT(app, (ConsoleCancelEventArgs)EventArgs.Empty);
                        break;
                    }
                }
            }
        }

        private static void Process_SIGTERM(object sender, EventArgs e)
        {
            if (!IsExiting)
                StopApp();
        }

        private static void Console_SIGINT(object sender, ConsoleCancelEventArgs e)
        {
            StopApp();

            Environment.Exit((int)ExitCode.OK);
        }

        private static void StopApp()
        {
            IsExiting = true;

            _logger?.Info("Exit requested, shutting down gracefully...");
            _intToken.Cancel();
            App?.StopAsync().Wait();
            _exitRdy.Wait();
            _logger?.Info("Shutdown complete.");
        }

        private static void ExitWithCode(ExitCode exitcode) => Environment.Exit((int)exitcode);
    }
}
