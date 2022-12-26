using Microsoft.EntityFrameworkCore.Storage.ValueConversion;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace VerdanskGameBot.GameServer.Db
{
    class DateTimeOffsetToDateTimeConverter : ValueConverter<DateTimeOffset, DateTime>
    {
        public DateTimeOffsetToDateTimeConverter()
            : base(
                toDb => toDb.UtcDateTime,
                fromDb => new DateTimeOffset(fromDb)
                )
        {
        }
    }
}
