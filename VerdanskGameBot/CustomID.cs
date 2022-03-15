using MessagePack;
using MessagePack.Resolvers;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace VerdanskGameBot
{
    [MessagePackObject]
    public class CustomID
    {
        [Key(0)]
        public string Source { get; set; }

        [Key(1)]
        public Dictionary<string, object> Options { get; set; }

        internal string Serialize()
        {
            return Encoding.Unicode.GetString(
                MessagePackSerializer.Serialize(this,
                MessagePackSerializerOptions.Standard
                .WithResolver(ContractlessStandardResolver.Instance)
                .WithCompression(MessagePackCompression.Lz4Block)));
        }

        internal static CustomID Deserialize(string json)
        {
            return MessagePackSerializer.Deserialize<CustomID>(
                Encoding.Unicode.GetBytes(json),
                MessagePackSerializerOptions.Standard
                .WithResolver(ContractlessStandardResolver.Instance)
                .WithCompression(MessagePackCompression.Lz4Block));
        }
    }
}
