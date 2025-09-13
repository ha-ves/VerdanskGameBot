using Discord;
using Discord.Interactions;
using Discord.WebSocket;
using HtmlAgilityPack;
using Jering.Javascript.NodeJS;
using Microsoft.EntityFrameworkCore;
using NLog;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;
using VerdanskGameBot.Ext;
using VerdanskGameBot.GameServer;
using VerdanskGameBot.GameServer.Db;
using VerdanskGameBot.GameServer.Db.Models;

namespace VerdanskGameBot.Commands.GameServer
{
    public partial class GameServerInteraction(
        DbContextOptions<GameServerDb> gsDbOpts,
        GamedigGamesJsonDocument supportGames
        )
        : InteractionModuleBase<SocketInteractionContext>
    {
        private async Task<bool> ValidateSlashCmdAsync(string logprefix)
        {
            if (Context.Channel is not ITextChannel)
            {
                _logger?.ConditionalTrace($"{logprefix} in non-text channel: {Context.Channel.GetChannelType()}");
                await RespondAsync("This command can only be used in text channels.", ephemeral: true);
                return false;
            }

            if (Context.Guild is null)
            {
                _logger?.ConditionalTrace($"{logprefix} in DM or private channel.");
                await RespondAsync("This command can only be used in a discord server.", ephemeral: true);
                return false;
            }

            _logger?.ConditionalDebug($"{logprefix} for guild [{Context.Guild}]({Context.Guild.Id}) ...");
            return true;
        }

        private async Task<bool> ValidateEntryParamsAsync(string? servername, string? messageUrl, string logprefix)
        {
            // Validate that exactly one parameter is provided
            if ((servername is null && messageUrl is null) || (servername is not null && messageUrl is not null))
            {
                _logger?.ConditionalTrace($"{logprefix} with invalid parameter combination.");
                await RespondAsync("Please provide either a server name OR a message URL, but NOT BOTH.", ephemeral: true);
                return false;
            }
            else if (servername is null && messageUrl is null)
            {
                _logger?.ConditionalTrace($"{logprefix} with no parameters?");
                await RespondAsync("Please provide either a server name OR a message URL.", ephemeral: true);
                return false;
            }

            return true;
        }

        private static bool ValidateState(string guidstr,
            out Guid guid, out (DateTime since, object? obj) state,
            Func<object?, bool>? stateCheck = null
            )
        {
            if (!Guid.TryParse(guidstr, out guid))
            {
                _logger?.ConditionalTrace($"Invalid custom id format: {guidstr}");
                state = default;
                return false;
            }

            if (!_state.TryGetValue(guid!, out state) && (stateCheck?.Invoke(state.obj) ?? true))
            {
                _logger?.ConditionalTrace($"State not found for GUID: {guid}");
                return false;
            }
            else if (DateTime.Now - state.since > _stateTimeout)
            {
                _logger?.ConditionalTrace($"State stale for GUID: {guid}, not continuing.");
                _state.Remove(guid);
                return false;
            }

            return true;
        }

        public async Task<bool> ValidateInputModalAsync(ServerInputModal modal, StringBuilder invalidstr)
        {
            var invalid = false;
            var gametype = modal.GameType!.Normalize().ToLowerInvariant();
            if (!_supportGames.RootElement.TryGetProperty(gametype, out _))
            {
                _logger?.ConditionalTrace($"Game type '{gametype}' is not supported.");
                invalidstr.AppendLine("- Game Type Not Supported. Try using other game type with the same protocol.");
                invalid = true;
            }
            else
            {
                modal.GameType = gametype;
            }

            // TODO: handle IPv4 and ipv6
            try
            {
                modal.IPs = await Dns.GetHostAddressesAsync(modal.HostIP!);
            }
            catch (Exception ex)
            {
                _logger?.ConditionalTrace(ex);
                modal.IPs = null;
            }

            if (modal.IPs is null or { Length: < 1 })
            {
                _logger?.ConditionalTrace($"Hostname/IP '{modal.HostIP}' could not be resolved.");
                invalidstr.AppendLine($"- Hostname/IP could not be resolved.");
                invalid = true;
            }

            if (!string.IsNullOrWhiteSpace(modal.GamePort)
                && ushort.TryParse(modal.GamePort, out modal.GamePortNum) is false)
            {
                _logger?.ConditionalTrace($"Invalid game port: {modal.GamePort}");
                invalidstr.AppendLine("- Game Port is invalid. Must be between 1 and 65535.");
                invalid = true;
            }

            if (!byte.TryParse(modal.UpdateMin, out modal.UpdateMinNum))
            {
                _logger?.ConditionalTrace($"Invalid update interval: {modal.UpdateMin}");
                invalidstr.AppendLine("- Update Interval is invalid.");
                invalid = true;
            }

            return !invalid;
        }

        /// <summary>
        /// Removes states from the internal collection that satisfy the specified predicate.
        /// </summary>
        /// <remarks>The method evaluates the predicate for each state in the collection and removes all
        /// states  for which the predicate returns <see langword="true"/>. The removal is performed during enumeration,
        /// and the method returns the removed states as an enumerable collection.</remarks>
        /// <param name="predicate">A function that determines whether a state should be removed. The function takes a tuple  containing the
        /// timestamp and associated object of the state and returns <see langword="true"/>  to remove the state;
        /// otherwise, <see langword="false"/>.</param>
        /// <returns>An enumerable collection of key-value pairs representing the removed states. Each key-value pair  contains
        /// the unique identifier of the state and the associated tuple with the timestamp and object,  or <see
        /// langword="null"/> if no object was associated.</returns>
        public static KeyValuePair<Guid, (DateTime since, object? obj)?>[]?
            RemoveStates(Func<(DateTime since, object? obj), bool> predicate)
            => _state.Where(s => predicate.Invoke(s.Value))?
                    .Select(s =>
                    {
                        _state.Remove(s.Key, out var v);
                        return new KeyValuePair<Guid, (DateTime since, object? obj)?>(s.Key, v);
                    })
                    .ToArray();

        private async Task<GameServerModel?> GetExistingServerAsync(string? logprefix = null,
            string? messageUrl = null, string? servername = null)
        {
            GameServerModel? foundServer = null;
            // Handle message URL parameter
            if (messageUrl is not null)
            {
                // Validate and parse message URL
                if (!Uri.TryCreate(messageUrl, UriKind.Absolute, out Uri? uri) ||
                    (!uri.Host.Equals("discord.com", StringComparison.OrdinalIgnoreCase) &&
                     !uri.Host.Equals("discordapp.com", StringComparison.OrdinalIgnoreCase)))
                {
                    _logger?.ConditionalTrace($"{logprefix} with invalid message URL: '{messageUrl}'");
                    await FollowupAsync("❌ Invalid Discord message URL. Please provide a valid Discord message link.\n" +
                                      "**How to get a message link:** Right-click on a message → Copy Message Link", ephemeral: true);
                }

                ulong guildId = 0; ulong chId = 0; ulong msgId = 0;
                // Parse Discord message URL format: https://discord.com/channels/{guild_id}/{channel_id}/{message_id}
                var pathSegments = uri!.AbsolutePath.Split('/', StringSplitOptions.RemoveEmptyEntries);
                if (pathSegments.Length < 4 || pathSegments[0] != "channels" ||
                    !ulong.TryParse(pathSegments[1], out guildId) ||
                    !ulong.TryParse(pathSegments[2], out chId) ||
                    !ulong.TryParse(pathSegments[3], out msgId))
                {
                    _logger?.ConditionalTrace($"{logprefix} with malformed message URL: '{messageUrl}'");
                    await FollowupAsync("❌ Malformed Discord message URL format.\n" +
                                      "**Expected format:** `https://discord.com/channels/{guild_id}/{channel_id}/{message_id}`\n" +
                                      "**How to get a message link:** Right-click on a message → Copy Message Link", ephemeral: true);
                }

                // Validate guild ID matches current guild
                if (guildId != Context.Guild.Id)
                {
                    _logger?.ConditionalTrace($"{logprefix} with message URL from different guild: {guildId}");
                    await FollowupAsync($"❌ The message URL is from a different Discord server.\n" +
                                      $"**Current server:** {Context.Guild.Name}\n" +
                                      "**Tip:** Make sure to copy the message link from this server.", ephemeral: true);
                }

                // Find server by channel and message IDs
                using var readOnlyDb = new GameServerReadOnlyDb(Context.Guild.Id, _gsDbOpts);
                try
                {
                    foundServer = readOnlyDb.GameServers.FirstOrDefault(
                                                s => s.ChannelId == chId && s.MessageId == msgId);
                }
                catch (Exception e)
                {
                    _logger?.ConditionalTrace(new DbNotSupportedException(Context.Guild.Id, e),
                        $"Failed to refresh game server for guild [{Context.Guild}].");
                    await FollowupAsync("Command failed. The bot encountered a problem for this discord server.", ephemeral: true);
                }

                if (foundServer is null)
                {
                    _logger?.ConditionalTrace($"{logprefix} but no server found for message URL: '{messageUrl}'");
                    await FollowupAsync("No game server found for the provided message link in this discord server.", ephemeral: true);
                }
            }
            else if (servername is not null)
            {
                servername = servername.Normalize().ToLowerInvariant();
                _logger?.ConditionalDebug($"{logprefix} for guild [{Context.Guild}]({Context.Guild.Id}) ...");
                if (char.IsAsciiLetter(servername[0]) && !servername.All(char.IsAsciiLetterOrDigit))
                {
                    _logger?.ConditionalTrace($"Invalid servername: '{servername}'");
                    await FollowupAsync($"***servername*** : `{servername}` is invalid." +
                        $"Only alphanumerics(a-z,0-9) are allowed and FIRST character MUST NOT be a number.", ephemeral: true);
                }

                // Use read-only context to find the server
                using var readOnlyDb = new GameServerReadOnlyDb(Context.Guild.Id, _gsDbOpts);
                try
                {
                    foundServer = readOnlyDb.GameServers.FirstOrDefault(s => s.ServerName == servername);
                }
                catch (Exception e)
                {
                    _logger?.ConditionalTrace(new DbNotSupportedException(Context.Guild.Id, e),
                        $"Failed to refresh game server for guild [{Context.Guild}].");
                    await FollowupAsync("Command failed. The bot encountered a problem for this discord server.", ephemeral: true);
                }

                if (foundServer is null)
                {
                    _logger?.ConditionalTrace($"{logprefix} but servername '{servername}' not found in this guild.");
                    await FollowupAsync($"No game server found with the name: `{servername}` in this discord server.", ephemeral: true);
                }
            }

            return foundServer;
        }

        private static (EmbedBuilder, ComponentBuilder) GetRetryBuilders(string customid, ServerInputModal modal, Guid guid)
        {
            var retryembed = new EmbedBuilder()
                        .WithTitle($"Failed to add '{modal.Name}'")
                        .AddField("Game Type", modal.GameType, true)
                        .AddField("Hostname / IP", modal.HostIP, true)
                        .AddField("Update Interval", modal.UpdateMin, true);

            if (!string.IsNullOrWhiteSpace(modal.GamePort))
                retryembed.AddField("Game Port", modal.GamePort, true);

            if (modal.Note?.Length > 0)
                retryembed.AddField("Note", modal.Note, true);

            var retrybtns = new ComponentBuilder()
                    .WithButton(
                        label: "Try Again", style: ButtonStyle.Primary,
                        emote: Emoji.Parse(":repeat:"),
                        customId: GetCustomId(customid, guid))
                    .WithButton(
                        label: "Supported Games List", style: ButtonStyle.Link,
                        emote: Emoji.Parse(":globe_with_meridians:"),
                        url: "https://github.com/gamedig/node-gamedig/blob/master/GAMES_LIST.md")
                    .WithButton(
                        label: "Check DNS Propagation", style: ButtonStyle.Link,
                        emote: Emoji.Parse(":globe_with_meridians:"),
                        url: "https://letmegooglethat.com/?q=dns+propagation+check");

            return (retryembed, retrybtns);
        }

        private static void SetServerValues(ServerInputModal modal, GameServerModel gs)
        {
            gs.GameType = modal.GameType;
            gs.GamePort = modal.GamePortNum;
            gs.IP = modal.IPs![0];
            gs.UpdateInterval = TimeSpan.FromMinutes(modal.UpdateMinNum);
            gs.Note = modal.Note;
        }

        private static void ResetQueried(GameServerModel gs)
        {
            gs.DisplayName = null; // reset display name to be regenerated
            gs.Description = null; // reset description to be regenerated
            gs.ImageUrl = null; // reset image url to be regenerated
            gs.IsOnline = false;
            gs.LastOnline = null;
            gs.GameLink = null;
            gs.GameLinkRemarks = null;
            gs.Players = null;
            gs.MaxPlayers = null;
            gs.LastUpdate = null;
        }

        public static void RegisterLogger(LogFactory logFactory) 
            => _logger = logFactory?.GetCurrentClassLogger();

        internal static string GetCustomId(string leadId, Guid guid)
            => $"{leadId}({guid.ToString("N").ToLowerInvariant()})";

        internal static void RegisterModals(InteractionService botInteractSvc)
        {
            botInteractSvc.AddModalInfo<ServerInputModal>();
        }
    }
}
