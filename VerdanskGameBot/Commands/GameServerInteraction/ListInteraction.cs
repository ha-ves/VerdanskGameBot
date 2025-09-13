using Discord;
using Discord.Interactions;
using Discord.Rest;
using Discord.WebSocket;
using Microsoft.EntityFrameworkCore;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using VerdanskGameBot.Ext;
using VerdanskGameBot.GameServer;
using VerdanskGameBot.GameServer.Db;
using VerdanskGameBot.GameServer.Db.Models;

namespace VerdanskGameBot.Commands.GameServer
{
    public partial class GameServerInteraction
    {
        #region Lists server in the server '/gameserver list'

        [SlashCommand(CmdSlash.List.Cmd, CmdSlash.List.Desc)]
        public async Task ServerListCmd_Exec()
        {
            string logprefix = $"User [{Context.User}] invoked '{CmdSlash.Prefix} {CmdSlash.List.Cmd}'";

            if (!await ValidateSlashCmdAsync(logprefix)) return;

            await DeferAsync(ephemeral: true);

            DateTimeOffset asOf;
            List<GameServerModel> allServers;
            using var readOnlyDb = new GameServerReadOnlyDb(Context.Guild.Id, _gsDbOpts);
            try
            {
                asOf = DateTimeOffset.UtcNow;
                allServers = [.. readOnlyDb.GameServers];
            }
            catch (Exception e)
            {
                _logger?.ConditionalTrace(new DbNotSupportedException(Context.Guild.Id, e),
                    $"Failed to list game servers for guild [{Context.Guild}].");
                await FollowupAsync("Command failed. The bot encountered a problem for this discord server.", ephemeral: true);
                return;
            }

            if (allServers.Count == 0)
            {
                await FollowupAsync("No game servers are currently being watched in this discord server.", ephemeral: true);
                return;
            }

            var guid = Guid.NewGuid();
            int currPageIdx = 0;

            var embed = new ListEmbedBuilder(Context, GetServersForPage(allServers, currPageIdx), allServers.Count, currPageIdx, asOf);
            var listbtns = new ListButtonsBuilder(guid, allServers.Count, currPageIdx);

            var svMsg = await FollowupAsync(components: listbtns.Build(), embed: embed.Build(), ephemeral: true);

            _state[guid] = (DateTime.Now, new ServerList(allServers, embed, listbtns, currPageIdx, svMsg, asOf));
        }

        #region Refresh button

        [ComponentInteraction(CmdSlash.List.RefreshBtnRegex, TreatAsRegex = true)]
        public async Task ServerListRefreshBtn_Exec(string guidstr)
        {
            if (!ValidateState(guidstr, out var guid, out var state, s => s is ServerList))
            {
                await RespondAsync("Invalid data / Interaction expired. Please try again from the beginning.", ephemeral: true);
                return;
            }

            var svList = (ServerList)state.obj!;
            if (!await svList.Semaphore.WaitAsync(0))
            {
                await RespondAsync("Another interaction is currently in progress. Please try again in a moment.", ephemeral: true);
                return;
            }

            try
            {
                await DeferAsync(ephemeral: true);

                using var readDb = new GameServerReadOnlyDb(Context.Guild.Id, _gsDbOpts);
                try
                {
                    svList.AsOf = DateTimeOffset.UtcNow;
                    svList.Servers = [.. readDb.GameServers];
                }
                catch (Exception e)
                {
                    _logger?.ConditionalTrace(new DbNotSupportedException(Context.Guild.Id, e),
                        $"Failed to refresh game server list for guild [{Context.Guild}].");
                    await FollowupAsync("Command failed. The bot encountered a problem for this discord server.", ephemeral: true);
                    return;
                }

                var currServers = svList.Servers.Skip(svList.CurrPageIdx * CmdSlash.List.ChunkSize)
                                                .Take(CmdSlash.List.ChunkSize).ToList();

                await UpdateListAsync(svList, guid, currServers, svList.CurrPageIdx, svList.Servers.Count);

                _state[guid] = (DateTime.Now, svList);
            }
            finally
            {
                _logger?.ConditionalTrace("Releasing svList semaphore");
                svList.Semaphore.Release();
            }
        }

        #endregion

        #region Navigation (First,Prev,Next,Last) buttons

        [ComponentInteraction(CmdSlash.List.NavBtnsRegex, TreatAsRegex = true)]
        public async Task ServerListNavsBtn_Exec(string reqIdx, string guidstr)
        {
            if (!ValidateState(guidstr, out var guid, out var state, s => s is ServerList)
                || !int.TryParse(reqIdx, out var reqIdxInt))
            {
                await RespondAsync("Invalid data / Interaction expired. Please try again from the beginning.", ephemeral: true);
                return;
            }

            var svList = (ServerList)state.obj!;
            if (!await svList.Semaphore.WaitAsync(0))
            {
                await RespondAsync("Another interaction is currently in progress. Please try again in a moment.", ephemeral: true);
                return;
            }

            try
            {
                await DeferAsync(ephemeral: true);

                svList.CurrPageIdx = GetValidPageIndex(reqIdxInt, svList.Servers.Count);
                var currServers = GetServersForPage(svList.Servers, svList.CurrPageIdx);

                await UpdateListAsync(svList, guid, currServers, svList.CurrPageIdx, svList.Servers.Count);

                _state[guid] = (DateTime.Now, svList);
            }
            finally
            {
                svList.Semaphore.Release();
            }
        }

        #endregion

        #endregion

        #region List server components

        internal class ServerList(
            List<GameServerModel> servers,
            ListEmbedBuilder embed,
            ListButtonsBuilder btns,
            int currpageidx,
            IUserMessage msg,
            DateTimeOffset asof)
        {
            internal SemaphoreSlim Semaphore { get; } = new(1, 1);
            internal List<GameServerModel> Servers { get; set; } = servers;
            public ListEmbedBuilder Embed { get; } = embed;
            public ListButtonsBuilder Buttons { get; } = btns;
            public int CurrPageIdx { get; set; } = currpageidx;
            public IUserMessage ListMessage { get; } = msg;
            public DateTimeOffset AsOf { get; set; } = asof;
        }

        private async Task UpdateListAsync(ServerList svList, Guid guid,
            List<GameServerModel> currServers, int pageIdx, int totalCount)
        {
            await svList.Embed.WithUpdateAsync(Context, currServers, totalCount, pageIdx, svList.AsOf);
            svList.Buttons.WithUpdate(totalCount, pageIdx, guid);

            await FollowupAsync(components: svList.Buttons.Build(), embed: svList.Embed.Build(), ephemeral: true);
        }

        #region Embed builder

        internal class ListEmbedBuilder : EmbedBuilder
        {
            private readonly string _fieldSeparator;

            public ListEmbedBuilder(IInteractionContext Context, List<GameServerModel> currServers,
                int totalCount, int currPageIdx, DateTimeOffset asOf)
            {
                _fieldSeparator = new('\u2014', 32);

                Title = $"List of Game Servers in {Context.Guild}";
                ThumbnailUrl = Context.Guild.IconUrl;
                Color = new Color(Random.Shared.Next(255), Random.Shared.Next(255), Random.Shared.Next(255));

                WithFooter($"Total {totalCount} game servers.");
                WithUpdateAsync(Context, currServers, totalCount, currPageIdx, asOf).Wait();
            }

            internal async Task<ListEmbedBuilder> WithUpdateAsync(IInteractionContext Context, 
                List<GameServerModel> currServers, int totalCount, int pageIdx, DateTimeOffset asOf)
            {
                Fields = [];
                Description = $"This game server list is updated as of <t:{asOf.ToUnixTimeSeconds()}:R>."
                    + Environment.NewLine + $"**{_fieldSeparator}**";

                if (totalCount == 0)
                {
                    Description += "No game servers are currently being watched in this discord server.";
                }
                else
                {
                    var haveNext = pageIdx < GetTotalPages(totalCount) - 1;
                    var middle = 1;
                    if (pageIdx > 0)
                    {
                        middle += 1;
                        Description += Environment.NewLine + new string('\u2002', 28) + "...";
                    }
                    if (haveNext)
                    {
                        middle += 1;
                    }

                    var startIdx = pageIdx * CmdSlash.List.ChunkSize;
                    var pageNum = startIdx;
                    for (var i = 0; i < Math.Min(25 - middle, currServers.Count); i++)
                    {
                        var server = currServers[i];
                        var chNameLen = (await Context.Guild.GetTextChannelAsync(server.ChannelId)).Name.Length + 2;

                        var displayName = server.DisplayName.FlattenNormalized(75, suffix: "...");
                        var jumpUrl = $"https://discord.com/channels/{Context.Guild.Id}/{server.ChannelId}/{server.MessageId}";

                        var gl = server.GameLink?.FlattenNormalized().ToShortMarkdown(out _, 20, suffix: "...");
                        var glnote = server.GameLinkRemarks?.FlattenNormalized().ToShortMarkdown(out _, 35, suffix: "...");

                        AddField($"{++pageNum}. {(server.IsOnline ? ":green_circle:" : ":red_circle:")} **{displayName}**",
                            $"┃ **{gl ?? "-- Server doesn't provide join link. --"}** ." 
                            + (gl is not null ? glnote : string.Empty)
                            + Environment.NewLine + $"┗ ({jumpUrl})");
                    }

                    if (haveNext)
                    {
                        var last = Fields.Last();
                        last.WithValue(last.Value + Environment.NewLine + new string('\u2002', 28) + "...");
                    }
                    
                    AddField($"**{_fieldSeparator }**", $"Showing servers {startIdx + 1} to " +
                        $"{pageNum} of total {totalCount} servers.");
                }

                var clockUnicode = Helper.ClockEmoji(asOf.Hour % 12, asOf.Minute / 30 * 30);
                Footer.Text = $"Total {totalCount} game servers. Updated as of -> {clockUnicode}";
                Timestamp = asOf;

                return this;
            }
        }

        #endregion

        #region Buttons builder

        internal class ListButtonsBuilder : ComponentBuilder
        {
            private readonly ButtonBuilder _refreshBtn, _firstBtn, _prevBtn, _pageInd, _nextBtn, _lastBtn;

            public ListButtonsBuilder(Guid guid, int totalCount, int pageIdx)
            {
                _firstBtn = new ButtonBuilder()
                {
                    Style = ButtonStyle.Primary,
                    Emote = Emoji.Parse(":arrow_double_up:"),
                    CustomId = GetCustomId(CmdSlash.Prefix + CustomIdSeparator 
                        + CmdSlash.List.NavBtns + $"(-2)", guid),
                };

                _prevBtn = new ButtonBuilder()
                {
                    Style = ButtonStyle.Primary,
                    Emote = Emoji.Parse(":arrow_up_small:"),
                };

                _pageInd = new ButtonBuilder()
                {
                    Style = ButtonStyle.Secondary,
                    CustomId = GetCustomId(CmdSlash.Prefix + CustomIdSeparator
                        + CmdSlash.List.NavBtns + "(-3)", guid),
                    IsDisabled = true,
                };

                _nextBtn = new ButtonBuilder()
                {
                    Style = ButtonStyle.Primary,
                    Emote = Emoji.Parse(":arrow_down_small:"),
                };

                _lastBtn = new ButtonBuilder()
                {
                    Style = ButtonStyle.Primary,
                    Emote = Emoji.Parse(":arrow_double_down:"),
                };

                _refreshBtn = new ButtonBuilder()
                {
                    Style = ButtonStyle.Primary,
                    Label = "Refresh",
                    Emote = Emoji.Parse(":arrows_clockwise:"),
                    CustomId = GetCustomId(CmdSlash.Prefix + CustomIdSeparator 
                        + CmdSlash.List.RefreshBtn, guid),
                };

                WithUpdate(totalCount, pageIdx, guid);

                WithButton(_firstBtn);
                WithButton(_prevBtn);
                WithButton(_pageInd);
                WithButton(_nextBtn);
                WithButton(_lastBtn);
                WithButton(_refreshBtn, 1);
            }

            internal ListButtonsBuilder WithUpdate(int totalCount, int pageIdx, Guid guid)
            {
                var numPages = GetTotalPages(totalCount);

                _firstBtn.IsDisabled = _prevBtn.IsDisabled = pageIdx == 0;

                _pageInd.Label = $"Page {pageIdx + 1} / {numPages}";

                _nextBtn.IsDisabled = _lastBtn.IsDisabled = pageIdx == numPages - 1;

                _refreshBtn.IsDisabled = false;

                _prevBtn.CustomId = GetCustomId(CmdSlash.Prefix + CustomIdSeparator
                                    + CmdSlash.List.NavBtns + $"({pageIdx - 1})", guid);

                _nextBtn.CustomId = GetCustomId(CmdSlash.Prefix + CustomIdSeparator
                                    + CmdSlash.List.NavBtns + $"({pageIdx + 1})", guid);

                _lastBtn.CustomId = GetCustomId(CmdSlash.Prefix + CustomIdSeparator
                                    + CmdSlash.List.NavBtns + $"({numPages + 1})", guid);

                return this;
            }

            internal ListButtonsBuilder WithUpdating()
            {
                _refreshBtn.IsDisabled = 
                    _firstBtn.IsDisabled = _prevBtn.IsDisabled =
                    _nextBtn.IsDisabled = _lastBtn.IsDisabled = true;

                return this;
            }
        }

        #region Pagination helpers

        private static int GetTotalPages(int totalCount)
        {
            return totalCount == 0 ? 1 : (int)Math.Ceiling((double)totalCount / CmdSlash.List.ChunkSize);
        }

        private static int GetValidPageIndex(int requestedPageIndex, int totalCount)
        {
            var maxPageIndex = GetTotalPages(totalCount) - 1;
            return Math.Max(0, Math.Min(requestedPageIndex, maxPageIndex));
        }

        private static List<GameServerModel> GetServersForPage(List<GameServerModel> allServers, int pageIdx)
        {
            return [.. allServers.Skip(pageIdx * CmdSlash.List.ChunkSize).Take(CmdSlash.List.ChunkSize)];
        }

        #endregion

        #endregion

        #endregion
    }
}
