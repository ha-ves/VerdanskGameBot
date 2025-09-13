using Discord;
using System;
using System.Threading.Tasks;
using VerdanskGameBot.GameServer;
using VerdanskGameBot.GameServer.Db;

namespace VerdanskGameBot.GameServer
{
    public static class GameServerDbExtensions
    {
        /// <summary>
        /// Set user context for auditing from Discord user
        /// </summary>
        public static void SetUserContext(this GameServerDb db, IUser user)
        {
            var userContext = $"{user.Username}#{user.Discriminator} ({user.GlobalName ?? "No Global Name"})";
            db.SetUserContext(user.Id, userContext);
        }

        /// <summary>
        /// Set user context for auditing from Discord interaction
        /// </summary>
        public static void SetUserContext(this GameServerDb db, IDiscordInteraction interaction)
        {
            var user = interaction.User;
            var userContext = $"{user.Username}#{user.Discriminator} ({user.GlobalName ?? "No Global Name"}) via {interaction.Type}";
            db.SetUserContext(user.Id, userContext);
        }

        /// <summary>
        /// Execute a database operation with automatic user context setting
        /// </summary>
        public static async Task<T> WithUserContextAsync<T>(this GameServerDb db, IUser user, Func<GameServerDb, Task<T>> operation)
        {
            db.SetUserContext(user);
            try
            {
                return await operation(db);
            }
            finally
            {
                db.ClearUserContext();
            }
        }

        /// <summary>
        /// Execute a database operation with automatic user context setting
        /// </summary>
        public static async Task WithUserContextAsync(this GameServerDb db, IUser user, Func<GameServerDb, Task> operation)
        {
            db.SetUserContext(user);
            try
            {
                await operation(db);
            }
            finally
            {
                db.ClearUserContext();
            }
        }

        /// <summary>
        /// Execute a database operation with automatic user context setting
        /// </summary>
        public static T WithUserContext<T>(this GameServerDb db, IUser user, Func<GameServerDb, T> operation)
        {
            db.SetUserContext(user);
            try
            {
                return operation(db);
            }
            finally
            {
                db.ClearUserContext();
            }
        }

        /// <summary>
        /// Execute a database operation with automatic user context setting
        /// </summary>
        public static void WithUserContext(this GameServerDb db, IUser user, Action<GameServerDb> operation)
        {
            db.SetUserContext(user);
            try
            {
                operation(db);
            }
            finally
            {
                db.ClearUserContext();
            }
        }

        /// <summary>
        /// Helper to format user context from Discord user for GameServerWatcher operations
        /// </summary>
        public static string FormatUserContext(this IUser user, string action)
        {
            var context = $"{user.Username}#{user.Discriminator} ({user.GlobalName ?? "No Global Name"})";
            if (!string.IsNullOrEmpty(action))
            {
                context += $" via {action}";
            }
            return context;
        }

        /// <summary>
        /// Helper to format user context from Discord interaction for GameServerWatcher operations
        /// </summary>
        public static string FormatUserContext(this IDiscordInteraction interaction, string action = "")
        {
            var user = interaction.User;
            var context = $"{user.Username}#{user.Discriminator} ({user.GlobalName ?? "No Global Name"}) via {interaction.Type}";
            if (!string.IsNullOrEmpty(action))
            {
                context += $" - {action}";
            }
            return context;
        }
    }
}