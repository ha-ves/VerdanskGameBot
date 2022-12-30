using Discord;
using Discord.Interactions;
using Discord.WebSocket;
using NLog;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Net.Sockets;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using VerdanskGameBot.Ext;
using VerdanskGameBot.GameServer;
using VerdanskGameBot.GameServer.Db;

namespace VerdanskGameBot.Command
{
    internal partial class CommandService
    {
        internal static async Task StartService(DiscordSocketClient botclient, CancellationToken canceltoken)
        {
            if (canceltoken.IsCancellationRequested)
                return;

            botclient.SlashCommandExecuted += SlashCommand_Executed;
            botclient.ModalSubmitted += ModalSubmit_Executed;
            botclient.ButtonExecuted += ButtonClick_Executed;

            await PostCommands(botclient, canceltoken);
        }

        #region Events / Run on Bot thread (SHOULD NOT BLOCK)

        private static async Task PostCommands(DiscordSocketClient bot, CancellationToken canceltoken)
        {
            if (canceltoken.IsCancellationRequested)
                return;

            
            var guild = bot.Guilds.First();

            if (guild == null)
            {
                Program.Log.Fatal("This bot is not invited to any discord server yet. Invite to your server first.");
                Environment.Exit(-(int)ExitCodes.BotNoGuild);
            }

            var failed = false;
            Program.Log.Debug($"Posting commands to guild {guild.Name}.");

            await guild.DeleteApplicationCommandsAsync();
            Commands.ForEach(async cmd =>
            {
                try
                {

                    await guild.CreateApplicationCommandAsync(cmd);
                    Program.Log.Trace($"/{cmd.Name} command posted to {guild}");
                }
                catch (Exception exc)
                {
                    Program.Log.Debug(exc, $"Failed to post /{cmd.Name} command to guild '{guild.Name}'.");
                    failed = true;
                }
            });

            if (!failed)
                Program.Log.Debug("Successfully posted commands to guild.");
            else
                Program.Log.Debug("Failed to post commands to guild. Watcher may still run but commands can't be received.");
        }

        internal static void UpdateGameServerStatusMessage(GameServer.GameServerModel gameserver)
        {
            var chan = (Program.BotClient.GetChannelAsync(gameserver.ChannelId).Result as ITextChannel);

            chan.ModifyMessageAsync(gameserver.MessageId, msg =>
            {
                msg.Content = "";
                msg.Embeds = new[] { new GameServerEmbedBuilder(gameserver, chan.GetMessageAsync(gameserver.MessageId).Result.Embeds.FirstOrDefault()).Build() };
            }).Wait();

            Program.Log.Trace($"Updated game server {{ {gameserver.ServerName} }}");
        }

        private static Task SlashCommand_Executed(SocketSlashCommand cmd)
        {
            try
            {
                Task.Run(() =>
                {
                    switch (cmd.CommandName)
                    {
                        case "server":
                            GameServerCmdHandler(Program.BotClient, cmd);
                            break;
                        default:
                            break;
                    }
                }, Program.ExitCancel.Token);
            }
            catch { return Task.FromException(new AppExitException()); }

            return Task.CompletedTask;
        }

        private static Task ButtonClick_Executed(SocketMessageComponent arg)
        {
            try
            {
                Task.Run(() =>
                {
                    var customid = CustomID.Deserialize(arg.Data.CustomId);

                    switch (customid.Source)
                    {
                        case CustomIDs.AddServerSource:
                            AddServerTryAgainHandler(arg, customid);
                            break;
                        case CustomIDs.ChangeServerSource:
                            ChangeServerTryAgainHandler(arg, customid);
                            break;
                        default:
                            break;
                    }
                }, Program.ExitCancel.Token);
            }
            catch { return Task.FromException(new AppExitException()); }

            return Task.CompletedTask;
        }

        private static Task ModalSubmit_Executed(SocketModal modal)
        {
            try
            {
                Task.Run(() =>
                {
                    var customid = CustomID.Deserialize(modal.Data.CustomId);

                    switch (customid.Source)
                    {
                        case CustomIDs.AddServerSource:
                            AddServerModalSubmitHandler(modal);
                            break;
                        case CustomIDs.ChangeServerSource:
                            ChangeServerModalSubmitHandler(modal);
                            break;
                        default:
                            break;
                    }
                }, Program.ExitCancel.Token);
            }
            catch { return Task.FromException(new AppExitException()); }

            return Task.CompletedTask;
        }

        #endregion

        #region Slash Command Handlers

        #region /server Command Handlers

        private static void GameServerCmdHandler(DiscordSocketClient bot, SocketSlashCommand cmd)
        {
            var subcmd = cmd.Data.Options.First();

            Program.Log.Debug($"User {{ {cmd.User} }} invoked {{ /{cmd.Data.Name} {subcmd.Name} }} command.");

            switch (subcmd.Name)
            {
                case "list":
                    GameServerListingHandler(cmd);
                    break;
                case "add":
                    GameServerAdderHandler(cmd, subcmd.Options);
                    break;
                case "movehere":
                    GameServerMoverHandler(bot, cmd, subcmd.Options);
                    break;
                case "remove":
                    GameServerRemovalHandler(bot, cmd, subcmd.Options);
                    break;
                case "change":
                    GameServerChangeHandler(cmd, subcmd.Options);
                    break;
                case "refresh":
                    GameServerRefreshHandler(cmd, subcmd.Options);
                    break;
                default:
                    cmd.RespondAsync("Command invalid.", ephemeral: true).Wait();
                    break;
            }

            Program.Log.Debug(cmd.HasResponded ? "^ Responded successfully." : "^ No response sent.");
        }

        #region refresh

        private static void GameServerRefreshHandler(SocketSlashCommand cmd, IReadOnlyCollection<SocketSlashCommandDataOption> options)
        {
            var servername = options.First().Value as string;

            GameServer.GameServerModel theserver;

            using (var db = new GameServerDb())
                theserver = db.GameServers.FirstOrDefault(srv => srv.ServerName == servername);

            if (theserver == null)
                cmd.RespondAsync($"There is no gameserver with name \"{servername}\". Please check the correct name.", ephemeral: true).Wait();
            else
            {
                cmd.DeferAsync(ephemeral: true).Wait();

                if (GameServerWatcher.RefreshWatcher(theserver))
                    cmd.FollowupAsync($"Refreshed game server `{servername}`'s status info.", ephemeral: true).Wait();
                else
                    cmd.FollowupAsync($"Can not refresh game server `{servername}`'s status info.", ephemeral: true).Wait();
            }
        }

        #endregion

        #region change

        private static void GameServerChangeHandler(SocketSlashCommand cmd, IReadOnlyCollection<SocketSlashCommandDataOption> options)
        {
            var servername = options.First().Value as string;

            GameServer.GameServerModel theserver;

            using (var db = new GameServerDb())
                theserver = db.GameServers.FirstOrDefault(srv => srv.ServerName == servername);

            if (theserver == null)
                cmd.RespondAsync($"There is no gameserver with name \"{servername}\". Please check the correct name.", ephemeral: true).Wait();
            else
                cmd.RespondWithModalAsync(new ChangeServerModalBuilder(theserver).Build()).Wait();
        }

        #endregion

        #region list

        private static void GameServerListingHandler(SocketSlashCommand cmd)
        {
            var guild = (cmd.Channel as SocketGuildChannel).Guild;

            List<GameServer.GameServerModel> gameservers;

            try
            {
                using (var db = new GameServerDb())
                    gameservers = db.GameServers.ToList();
            }
            catch (Exception)
            {
                cmd.RespondAsync("Something is wrong, please try again.", ephemeral: true);

                return;
            }

            if (gameservers.Any())
            {
                cmd.RespondAsync(embed: new EmbedBuilder()
                    .WithColor(Discord.Color.Gold)
                    .WithTitle($":desktop: {guild.Name}'s Game Server Watch List")
                    .WithThumbnailUrl(guild.IconUrl)
                    .AddField($"{gameservers.Count} Game Servers", $"There are {gameservers.Count} game servers in watch list. Fetching servers...")
                    .Build()
                , ephemeral: true).Wait();

                var embeds = new List<Embed>();

                if(Parallel.ForEach(gameservers, gameserver => embeds.Add(new GameServerEmbedBuilder(gameserver).Build())).IsCompleted)
                    cmd.ModifyOriginalResponseAsync(msg => msg.Embeds = embeds.ToArray()).Wait();
            }
            else
            {
                cmd.RespondAsync(embed: new EmbedBuilder()
                    .WithColor(Discord.Color.Gold)
                    .WithTitle($":desktop: {guild.Name}'s Game Server Watch List")
                    .WithThumbnailUrl(guild.IconUrl)
                    .AddField("Empty", "No game server in watch list.")
                    .Build()
                , ephemeral: true).Wait();
            }
        }

        #endregion

        #region add

        private static void GameServerAdderHandler(SocketSlashCommand cmd, IReadOnlyCollection<SocketSlashCommandDataOption> options)
        {
            var guild = (cmd.Channel as SocketGuildChannel).Guild;
            var servername = options.First().Value as string;

            using (var db = new GameServerDb())
            {
                if (db.GameServers.Any(server => server.ServerName == servername))
                {
                    Program.Log.Warn($"{servername} already exist in watch list database. Not added.");
                    cmd.RespondAsync($"Failed to add game server because{Environment.NewLine}" +
                        $"servername : `{servername}`{Environment.NewLine}" +
                        $"already exist in watch list.", ephemeral: true).Wait();
                    return;
                }
            }

            if (!servername.All(ch => char.IsLetterOrDigit(ch) && char.IsLower(ch)) || servername.Length > 22)
            {
                cmd.RespondAsync($"***servername*** : `{servername}` is invalid.{Environment.NewLine}" +
                    "Servername must be alphabets only (a-z) without whitespaces and up to 22 letters",
                    ephemeral: true).Wait();
                return;
            }

            cmd.RespondWithModalAsync(new AddServerModalBuilder(servername).Build()).Wait();
        }

        #endregion

        #region move

        private static void GameServerMoverHandler(DiscordSocketClient bot, SocketSlashCommand cmd, IReadOnlyCollection<SocketSlashCommandDataOption> options)
        {
            GameServer.GameServerModel theserver;

            using (var db = new GameServerDb())
                theserver = db.GameServers.FirstOrDefault(srv => srv.ServerName == options.First().Value as string);

            if (theserver == null)
            {
                cmd.ModifyOriginalResponseAsync(msg => msg.Content = $"**No game server with name `{options.First().Value}` found. Try again.**").Wait();
                Program.Log.Debug($"**No game server with name `{options.First().Value}` found. Try again.**");
            }

            cmd.RespondAsync($"**Moving game server with name `{options.First().Value}` to (<#{cmd.Channel.Id}>) ...**", ephemeral: true);
            Program.Log.Debug($"**Moving game server with name `{options.First().Value}` to (<#{cmd.Channel.Id}>) ...**");

            if (!GameServerWatcher.PauseUpdate(theserver))
            {
                cmd.ModifyOriginalResponseAsync(msg => msg.Content = $"**Can not move game server with name `{options.First().Value}` to this channel. Try again.**").Wait();
                Program.Log.Debug($"**Can not move game server with name `{options.First().Value}` to this channel. Try again.**");

                return;
            }

            var olmsg = (bot.GetChannelAsync(theserver.ChannelId).Result as ITextChannel).GetMessageAsync(theserver.MessageId).Result;
            var oldembed = olmsg.Embeds.First() as Embed;

            theserver.ChannelId = cmd.Channel.Id;
            theserver.MessageId = cmd.Channel.SendMessageAsync(embed: oldembed).Result.Id;

            using (var db = new GameServerDb())
            {
                db.Update(theserver);
                db.SaveChanges();
            }

            olmsg.DeleteAsync().Wait();

            cmd.ModifyOriginalResponseAsync(msg => msg.Content = $"**Moved game server with name `{options.First().Value}` to (<#{cmd.Channel.Id}>).**").Wait();
            Program.Log.Debug($"**Moved game server with name `{options.First().Value}` to (<#{cmd.Channel.Id}>).**");

            GameServerWatcher.ResumeUpdate(theserver);
        }

        #endregion

        #region remove

        private static void GameServerRemovalHandler(DiscordSocketClient bot, SocketSlashCommand cmd, IReadOnlyCollection<SocketSlashCommandDataOption> options)
        {
            GameServer.GameServerModel theserver;

            using (var db = new GameServerDb())
            {
                theserver = db.GameServers.First(srv => srv.ServerName == options.First().Value as string);

                db.Remove(theserver);
                db.SaveChanges();
            }

            GameServerWatcher.RemoveWatcher(theserver);

            (bot.GetChannelAsync(theserver.ChannelId).Result as ITextChannel).DeleteMessageAsync(theserver.MessageId).Wait();
        }

        #endregion

        #endregion

        #endregion

        #region Modal Submit Handlers

        private static void ChangeServerModalSubmitHandler(SocketModal modal)
        {
            var guild = (modal.Channel as SocketTextChannel).Guild;
            var components = modal.Data.Components;

            var customid = CustomID.Deserialize(modal.Data.CustomId);
            string servername = customid.Options[CustomIDs.ServernameOption];

            Program.Log.Debug($"User {modal.User} requested to change game server \"{servername}\" for guild \"{guild}\"...");

            modal.DeferAsync(ephemeral: true).Wait();

            var changeresult = GameServerWatcher.ChangeGameServer(modal);

            if (changeresult.IsCompletedSuccessfully)
            {
                Program.Log.Info($"User {{ {modal.User} }} Changed game server \"{servername}\" details for guild \"{guild}\".");

                modal.FollowupAsync($"Changed game server \"{servername}\"'s details.", ephemeral: true).Wait();
            }
            else
            {
                string errormsg = "";
                switch (changeresult.Exception.GetBaseException())
                {
                    case GamePortFormatException formexc:
                        errormsg = $"Failed to add game server ***{servername}***, Game port number is not valid.";
                        break;
                    case UpdateIntervalFormatException formexc:
                        errormsg = $"Failed to add game server ***{servername}***, Update interval is not valid.";
                        break;
                    case InvalidOperationException invopexc:
                        errormsg = $"Failed to find game server on `{components.First(cmp => cmp.CustomId == CustomIDs.HostIPPort.ToString("d")).Value}`, Make sure it is discoverable publicly and try again.";
                        break;
                    default:
                        errormsg = "Failed to change game server, Try again.";
                        break;
                }

                Program.Log.Debug(errormsg);

                customid.Options.Add(CustomIDs.ButtonOption, CustomIDs.TryAgainButton.ToString("d"));

                modal.FollowupAsync(errormsg, components: new ComponentBuilder()
                    .AddRow(new ActionRowBuilder()
                        .AddComponent(new ButtonBuilder(label: "Public IP Address", emote: Emoji.Parse(":globe_with_meridians:"), style: ButtonStyle.Link,
                        url: "https://letmegooglethat.com/?q=how+to+get+public+ip+address").Build())
                        .AddComponent(new ButtonBuilder(label: "Check DNS Propagation", emote: Emoji.Parse(":globe_with_meridians:"), style: ButtonStyle.Link,
                        url: "https://letmegooglethat.com/?q=dns+propagation+check").Build()))
                    .AddRow(new ActionRowBuilder()
                        .AddComponent(new ButtonBuilder(label: "Try Again", emote: Emoji.Parse(":repeat:"), style: ButtonStyle.Primary,
                        customId: new CustomID { Source = customid.Source, Options = customid.Options }.Serialize()).Build()))
                    .Build(), ephemeral: true).Wait();
            }
        }

        private static void AddServerModalSubmitHandler(SocketModal modal)
        {
            var guild = (modal.Channel as SocketTextChannel).Guild;
            var components = modal.Data.Components;

            var customid = CustomID.Deserialize(modal.Data.CustomId);
            string servername = customid.Options[CustomIDs.ServernameOption];

            Program.Log.Debug($"User {modal.User} requested to add game server \"{servername}\" to watch list for guild \"{guild}\"...");

            modal.DeferAsync(ephemeral: true).Wait();
            var placeholder = modal.Channel.SendMessageAsync("***Adding a game server to watch list...***").Result;

            var addresult = GameServerWatcher.AddGameServer(modal, placeholder);

            if (addresult.IsCompletedSuccessfully)
            {
                Program.Log.Info($"User {{ {modal.User} }} Added game server \"{servername}\" to watch list for guild \"{guild}\".");

                modal.FollowupAsync($"<@{modal.User.Id}> Added a game server to watch list on this channel (<#{modal.Channel.Id}>).").Wait();
                placeholder.ModifyAsync(msg =>
                {
                    msg.Content = "*Now checking if the server is online...*";
                    msg.Embed = new GameServerEmbedBuilder(addresult.Result).Build();
                }).Wait();

                var cancel = new CancellationTokenSource(5000);

                Task.Run(() =>
                {
                    while (!addresult.Result.IsOnline && !cancel.IsCancellationRequested)
                    {
                        Task.Delay(250).Wait();
                        Debug.WriteLine("Waiting for server to came up as online...");
                    }

                    if(cancel.IsCancellationRequested)
                        Program.Log.Debug($"TimedOut waiting for game server {{ {servername} }} to come up online.");
                }).Wait();

                placeholder.ModifyAsync(msg =>
                {
                    msg.Content = "";
                    msg.Embed = new GameServerEmbedBuilder(addresult.Result).Build();
                }).Wait();
            }
            else
            {
                string errormsg = "";
                switch (addresult.Exception.GetBaseException())
                {
                    case GamePortFormatException formexc:
                        errormsg = $"Failed to add game server ***{servername}***, Game port number is not valid.";
                        break;
                    case UpdateIntervalFormatException formexc:
                        errormsg = $"Failed to add game server ***{servername}***, Update interval is not valid.";
                        break;
                    case InvalidOperationException invopexc:
                        errormsg = $"Failed to find game server on `{components.First(cmp => cmp.CustomId == CustomIDs.HostIPPort.ToString("d")).Value}`, Make sure it is discoverable publicly and try again.";
                        break;
                    case AlreadyExistException existexc:
                        errormsg = "Game server with the same Public IP and Port exist. Can't add the same server to watch list more than one instance.";
                        break;
                    default:
                        errormsg = "Failed to add game server, Try again.";
                        break;
                }

                Program.Log.Debug(errormsg);

                customid.Options.Add(CustomIDs.ButtonOption, CustomIDs.TryAgainButton.ToString("d"));

                placeholder.DeleteAsync().Wait();

                modal.FollowupAsync(errormsg, components: new ComponentBuilder()
                    .AddRow(new ActionRowBuilder()
                        .AddComponent(new ButtonBuilder(label: "Public IP Address", emote: Emoji.Parse(":globe_with_meridians:"), style: ButtonStyle.Link,
                        url: "https://letmegooglethat.com/?q=how+to+get+public+ip+address").Build())
                        .AddComponent(new ButtonBuilder(label: "Check DNS Propagation", emote: Emoji.Parse(":globe_with_meridians:"), style: ButtonStyle.Link,
                        url: "https://letmegooglethat.com/?q=dns+propagation+check").Build()))
                    .AddRow(new ActionRowBuilder()
                        .AddComponent(new ButtonBuilder(label: "Try Again", emote: Emoji.Parse(":repeat:"), style: ButtonStyle.Primary,
                        customId: new CustomID{ Source = customid.Source, Options = customid.Options }.Serialize()).Build()))
                    .Build(), ephemeral: true).Wait();
            }
        }

        #endregion

        #region Button Handlers

        private static void ChangeServerTryAgainHandler(SocketMessageComponent arg, CustomID customid)
        {
            if (customid.Options[CustomIDs.ButtonOption] == CustomIDs.TryAgainButton.ToString("d"))
            {
                Program.Log.Debug($"User {arg.User} requested retry...");

                GameServer.GameServerModel theserver;

                using (var db = new GameServerDb())
                    theserver = db.GameServers.FirstOrDefault(srv => srv.ServerName == customid.Options[CustomIDs.ServernameOption]);

                arg.RespondWithModalAsync(new ChangeServerModalBuilder(theserver).Build()).Wait();
            }
        }

        private static void AddServerTryAgainHandler(SocketMessageComponent arg, CustomID customid)
        {
            if (customid.Options[CustomIDs.ButtonOption] == CustomIDs.TryAgainButton.ToString("d"))
            {
                Program.Log.Debug($"User {arg.User} requested retry...");
                var name = customid.Options[CustomIDs.ServernameOption];
                arg.RespondWithModalAsync(new AddServerModalBuilder(name).Build()).Wait();
            }
        }

        #endregion

    }
}
