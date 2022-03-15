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

namespace VerdanskGameBot
{
    partial class CommandService
    {
        internal CommandService(DiscordSocketClient botclient)
        {
            BotClient = botclient;

            botclient.SlashCommandExecuted += SlashCommandExecuted;
            botclient.ModalSubmitted += ModalSubmitted;
            botclient.ButtonExecuted += ButtonExecuted;

            PostCommands(Commands);
        }

        //temp to guild verdansk
        DiscordSocketClient BotClient;

        internal async Task PostCommands(List<SlashCommandBuilder> commands)
        {
            var verdansk = BotClient.Guilds.Where(guild => guild.Id == 790540532714831882).First();

            try
            {
                Parallel.ForEach(commands, cmd =>
                {
                    verdansk.BulkOverwriteApplicationCommandAsync(new ApplicationCommandProperties[] { cmd.Build() }).Wait();
                });
            }
            catch (Discord.Net.HttpException exc)
            {
                Debugger.Break();
            }
        }

        async Task SlashCommandExecuted(SocketSlashCommand cmd)
        {
            switch (cmd.CommandName)
            {
                case "server":
                    await GameServerCmdHandler(cmd);
                    break;
                default:
                    break;
            }
        }

        async Task GameServerCmdHandler(SocketSlashCommand cmd)
        {
            var subcmd = cmd.Data.Options.First();

            Program.Log.Debug($"User {{ {cmd.User} }} invoked {{ /{cmd.Data.Name} {subcmd.Name} }} command.");

            switch (subcmd.Name)
            {
                case "list":
                    await GameServerListingHandler(cmd);
                    break;
                case "add":
                    await GameServerAdderHandler(cmd, subcmd.Options);
                    break;
                case "movehere":
                    await GameServerMoverHandler(cmd, subcmd.Options);
                    break;
                case "remove":
                    await GameServerRemovalHandler(cmd, subcmd.Options);
                    break;
                default:
                    await cmd.RespondAsync("Command invalid.", ephemeral: true);
                    break;
            }

            if (cmd.HasResponded) Program.Log.Debug("^ Responded successfully.");
        }

        internal async Task GameServerUpdate(GameServerModel gameserver)
        {
            await (await BotClient.GetChannelAsync(gameserver.ChannelId) as ITextChannel)
                .ModifyMessageAsync(gameserver.MessageId, async msg =>
                {
                    msg.Content = "";
                    msg.Embed = (await new GameServerEmbedBuilder().Get(gameserver)).Build();
                });
        }

        async Task GameServerListingHandler(SocketSlashCommand cmd)
        {
            var guild = (cmd.Channel as SocketGuildChannel).Guild;
            using (var db = new GameServersDb())
            {
                if (db.GameServers.Count() > 0)
                {
                    var listing = await cmd.FollowupAsync(embed: new EmbedBuilder()
                        .WithColor(Discord.Color.Gold)
                        .WithTitle($":desktop: {guild.Name}'s Game Server Watch List")
                        .WithThumbnailUrl(guild.IconUrl)
                        .AddField($"{db.GameServers.Count()} Game Servers", $"There are {db.GameServers.Count()} game servers in watch list. Fetching servers...")
                        .Build()
                    , ephemeral: true);

                    db.GameServers.ToList().ForEach(async gameserver =>
                    {
                        await listing.ModifyAsync(async msg => msg.Embeds = msg.Embeds.Value.Append(
                            (await new GameServerEmbedBuilder().Get(gameserver)).Build()).ToArray());
                    });
                }
                else
                {
                    await cmd.RespondAsync(embed: new EmbedBuilder()
                        .WithColor(Discord.Color.Gold)
                        .WithTitle($":desktop: {guild.Name}'s Game Server Watch List")
                        .WithThumbnailUrl(guild.IconUrl)
                        .AddField("Empty", "No game server in watch list.")
                        .Build()
                    , ephemeral: true);
                }
            }
        }

        async Task GameServerAdderHandler(SocketSlashCommand cmd, IReadOnlyCollection<SocketSlashCommandDataOption> options)
        {
            var guild = (cmd.Channel as SocketGuildChannel).Guild;
            var servername = options.First().Value as string;

            using (var db = new GameServersDb())
            {
                if (db.GameServers.Any(server => server.ServerName == servername))
                {
                    Program.Log.Warn($"{servername} already exist in watch list database. Not added.");
                    await cmd.RespondAsync($"Failed to add game server because\r\n" +
                        $"servername : `{servername}`\r\n" +
                        $"already exist in watch list.", ephemeral: true);
                    return;
                }
            }

            if (!servername.All(ch => char.IsLetterOrDigit(ch) && char.IsLower(ch)) || servername.Length > 22)
            {
                await cmd.RespondAsync($"***servername*** : `{servername}` is invalid.\r\n" +
                    "Servername must be alphabets only (a-z) without whitespaces and up to 22 letters",
                    ephemeral: true);
                return;
            }

            await cmd.RespondWithModalAsync((await new AddServerModalBuilder().Get(servername)).Build());
        }

        async Task GameServerMoverHandler(SocketSlashCommand cmd, IReadOnlyCollection<SocketSlashCommandDataOption> options)
        {
            using (var db = new GameServersDb())
            {
                var theserver = await Task.Run(() => db.GameServers.First(srv => srv.ServerName == options.First().Value as string));

                var oldembed = (await ((await BotClient.GetChannelAsync(theserver.ChannelId)) as ITextChannel).GetMessageAsync(theserver.MessageId)).Embeds.First() as Embed;

                await cmd.Channel.SendMessageAsync(embed: oldembed);
                theserver.ChannelId = cmd.Channel.Id;
            }
        }

        async Task GameServerRemovalHandler(SocketSlashCommand cmd, IReadOnlyCollection<SocketSlashCommandDataOption> options)
        {
            using (var db = new GameServersDb())
            {
                var theserver = await Task.Run(() => db.GameServers.First(srv => srv.ServerName == options.First().Value as string));

                await GameServerWatcher.RemoveAsync(theserver);
                
                db.Remove(theserver);
                await db.SaveChangesAsync();

                await ((await BotClient.GetChannelAsync(theserver.ChannelId)) as ITextChannel).DeleteMessageAsync(theserver.MessageId);
            }
        }

        async Task ModalSubmitted(SocketModal modal)
        {
            var guild = (modal.Channel as SocketTextChannel).Guild;
            var components = modal.Data.Components;

            var customid = CustomID.Deserialize(modal.Data.CustomId);

            switch (customid.Source)
            {
                case "addserver":
                    Program.Log.Debug($"User {modal.User} requested to add game server \"{customid.Options["servername"]}\" to watch list for guild \"{guild}\"...");

                    var host_ip = components.First(cmp => cmp.CustomId == "host_ip").Value;
                    var gameport = components.First(cmp => cmp.CustomId == "game_port").Value;
                    var rconport = components.First(cmp => cmp.CustomId == "rcon_port").Value;

                    await modal.DeferAsync(true);
                    var placeholder = await modal.Channel.SendMessageAsync("***Adding a game server to watch list...***");

                    var addresult = await GameServerWatcher.Add(modal, placeholder);

                    if (addresult.IsCompletedSuccessfully)
                    {
                        Program.Log.Info($"User {{ {modal.User} }} Added game server \"{customid.Options["servername"]}\" to watch list for guild \"{guild}\".");

                        await modal.FollowupAsync($"{modal.User.ToString()} Added ***{customid.Options["servername"]}*** to watch list on this channel (<#{modal.Channel.Id}>).");
                        await placeholder.ModifyAsync(msg => msg.Content = "*Now checking if the server is online...*");

                        var canceltoken = new CancellationTokenSource();

                        await Task.WhenAny(Task.Run(() =>
                        {
                            while (!addresult.Result.IsOnline) ;
                        }, canceltoken.Token), Task.Run(async () =>
                        {
                            for (int i = 5; i > 0; i--)
                            {
                                await Task.Delay(1000);
                                await placeholder.ModifyAsync(msg => msg.Content = $"*Now checking if the server is online... **{i}***");
                            }
                        }, canceltoken.Token));
                        canceltoken.Cancel();

                        await placeholder.ModifyAsync(async msg =>
                        {
                            msg.Content = "";
                            msg.Embed = (await new GameServerEmbedBuilder().Get(addresult.Result)).Build();
                        });
                    }
                    else
                    {
                        string errormsg = "";
                        switch (addresult.Exception.GetBaseException())
                        {
                            case FormatException formexc:
                                errormsg = $"Failed to add game server ***{customid.Options["servername"]}***, Game/RCON port number is not valid.";
                                break;
                            case InvalidOperationException invopexc:
                                errormsg = $"Failed to find game server on `{host_ip}`, Make sure it is discoverable publicly and try again.";
                                break;
                            case AlreadyExistException existexc:
                                errormsg = "Game server with the same Public IP and Port exist. Can't add the same server to watch list more than one instance.";
                                break;
                            default:
                                errormsg = "Failed to add game server, Try again.";
                                break;
                        }

                        Program.Log.Debug(errormsg);

                        customid.Options.Add("btn", "try-again");

                        await placeholder.DeleteAsync();

                        await modal.ModifyOriginalResponseAsync(msg =>
                        {
                            msg.Content = errormsg;
                            msg.Components = new ComponentBuilder()
                            .AddRow(new ActionRowBuilder()
                                .AddComponent(new ButtonBuilder(label: "Public IP Address", emote: Emoji.Parse(":globe_with_meridians:"), style: ButtonStyle.Link,
                                url: "https://letmegooglethat.com/?q=how+to+get+public+ip+address").Build())
                                .AddComponent(new ButtonBuilder(label: "Check DNS Propagation", emote: Emoji.Parse(":globe_with_meridians:"), style: ButtonStyle.Link,
                                url: "https://letmegooglethat.com/?q=dns+propagation+check").Build()))
                            .AddRow(new ActionRowBuilder()
                                .AddComponent(new ButtonBuilder(label: "Try Again", emote: Emoji.Parse(":repeat:"), style: ButtonStyle.Primary,
                                customId: new CustomID { Source = customid.Source, Options = customid.Options }.Serialize()).Build()))
                            .Build();
                        });

                        //await modal.FollowupAsync(text: errormsg, components: new ComponentBuilder()
                        //    .AddRow(new ActionRowBuilder()
                        //        .AddComponent(new ButtonBuilder(label: "Public IP Address", emote: Emoji.Parse(":globe_with_meridians:"), style: ButtonStyle.Link,
                        //        url: "https://letmegooglethat.com/?q=how+to+get+public+ip+address").Build())
                        //        .AddComponent(new ButtonBuilder(label: "Check DNS Propagation", emote: Emoji.Parse(":globe_with_meridians:"), style: ButtonStyle.Link,
                        //        url: "https://letmegooglethat.com/?q=dns+propagation+check").Build()))
                        //    .AddRow(new ActionRowBuilder()
                        //        .AddComponent(new ButtonBuilder(label: "Try Again", emote: Emoji.Parse(":repeat:"), style: ButtonStyle.Primary,
                        //        customId: new CustomID { Source = customid.Source, Options = customid.Options }.Serialize()).Build()))
                        //    .Build(), ephemeral: true);
                    }
                    break;
                default:
                    break;
            }
        }

        async Task ButtonExecuted(SocketMessageComponent arg)
        {
            var customid = CustomID.Deserialize(arg.Data.CustomId);

            switch (customid.Source)
            {
                case "addserver":
                    if(customid.Options["btn"] as string == "try-again")
                    {
                        Program.Log.Debug($"User {arg.User} requested retry...");
                        await arg.RespondWithModalAsync((await new AddServerModalBuilder().Get(customid.Options["servername"] as string)).Build());
                    }
                    break;
                default:
                    break;
            }
        }

    }
}
