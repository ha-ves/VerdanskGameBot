namespace VerdanskGameBot.GameServer.Db
{
    public enum DbTypes
    {
        SQLite = 1,
        MySql,
        SqlServer,
        PostgreSql
    }

    public class DbNotSupportedException(ulong guildId, Exception? innerEx = null)
        : NotSupportedException(null, innerEx)
    {
        public override string Message =>
            $"Database corrupted or not supported or not properly initialized for guild ({guildId:X2})." +
            $"Either the database has been modified externally, " +
            $"or this version of the bot is incompatible with the existing database.";
    }
}
