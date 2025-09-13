using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace VerdanskGameBot.GameServer.Db.MigrationHandler
{
    internal class MigrationGuildHex(ICurrentDbContext dbCtx)
    {
        const string str = nameof(GameServerDb.GuildHex);
        internal static readonly string VarName = $"{char.ToLower(str[0])}{str[1..]}";

        public static string GetString(GameServerDb ctx) => ctx.GuildHex ?? $"{{{VarName}}}";

        public override string ToString() => GetString((GameServerDb)dbCtx.Context);

        public static implicit operator string(MigrationGuildHex mg) => mg.ToString();

        public override bool Equals(object? other)
            => other is MigrationGuildHex mg && ToString() == mg.ToString();

        public override int GetHashCode()
        {
            var hash = new HashCode();
            hash.Add(ToString());
            return hash.ToHashCode();
        }
    }
}
