using Microsoft.EntityFrameworkCore.Design;
using Microsoft.EntityFrameworkCore.Design.Internal;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Metadata;
using Microsoft.EntityFrameworkCore.Migrations;
using Microsoft.EntityFrameworkCore.Migrations.Design;
using Microsoft.EntityFrameworkCore.Migrations.Operations;
using Microsoft.EntityFrameworkCore.Storage;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace VerdanskGameBot.GameServer.Db.MigrationHandler
{
    internal partial class IntplStringCSharpHelper
        (ITypeMappingSource typeMappingSource) : CSharpHelper(typeMappingSource)
    {
        public override string Literal(string? value)
        {
            if (value != null && IsInterpolatedString(value))
                return LiteralIntpl(value);
            return base.Literal(value);
        }

        private static bool IsInterpolatedString(string value)
        {
            // Look for {variableName} patterns
            return IntplStringRegEx().IsMatch(value);
        }

        public static string LiteralIntpl(string value)
        // do not output @"" syntax as in Migrations this can get indented at a newline and so add spaces to the literal
        => value is not null
            ? new StringBuilder(value)
                .Replace("\\", @"\\")
                .Replace("\0", @"\0")
                .Replace("\n", @"\n")
                .Replace("\r", @"\r")
                .Replace("\"", "\\\"")
                .Insert(0, '"')
                .Insert(0, '$')
                .Append('"')
                .ToString()
            : "null";

        [System.Text.RegularExpressions.GeneratedRegex(@"\{[^}]+\}")]
        private static partial System.Text.RegularExpressions.Regex IntplStringRegEx();
    }

    internal partial class PerGuildMigrationsGenerator(
        MigrationsCodeGeneratorDependencies dependencies, 
        CSharpMigrationsGeneratorDependencies csharpDependencies) 
        : CSharpMigrationsGenerator(dependencies, csharpDependencies)
    {
        private static string ModifyPrimaryConstructor(string str)
        {
            var replacement = $"internal $2({nameof(MigrationGuildHex)} {MigrationGuildHex.VarName}) : $3";
            return ClassDeclarationRegex().Replace(str, replacement, 1);
        }

        public override string GenerateMigration(string? migrationNamespace, string migrationName,
            IReadOnlyList<MigrationOperation> upOperations, IReadOnlyList<MigrationOperation> downOperations)
        {
            var str = base.GenerateMigration(migrationNamespace, migrationName, upOperations, downOperations);
            return ModifyPrimaryConstructor(str);
        }

        public override string GenerateSnapshot(string? modelSnapshotNamespace, Type contextType, string modelSnapshotName, IModel model)
        {
            var str = base.GenerateSnapshot(modelSnapshotNamespace, contextType, modelSnapshotName, model);
            return ModifyPrimaryConstructor(str);
        }

        [System.Text.RegularExpressions.GeneratedRegex(@"(public\s+)?(partial\s+class\s+\w+)\s*:\s*(Migration|ModelSnapshot)")]
        private static partial System.Text.RegularExpressions.Regex ClassDeclarationRegex();
    }
}
