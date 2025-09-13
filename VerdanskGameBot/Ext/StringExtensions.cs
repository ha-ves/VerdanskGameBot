using Discord;
using Jering.Javascript.NodeJS;
using Markdig;
using Markdig.Parsers;
using Markdig.Renderers;
using Markdig.Renderers.Normalize;
using Markdig.Renderers.Roundtrip;
using Markdig.Syntax;
using Markdig.Syntax.Inlines;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using NLog;
using NLog.Config;
using NLog.Extensions.Logging;
using NLog.Layouts;
using NLog.Targets;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;
using System.Xml.Linq;
using VerdanskGameBot.GameServer.Db;
using VerdanskGameBot.GameServer.Db.Models;
using static System.Net.Mime.MediaTypeNames;

namespace VerdanskGameBot.Ext
{
    public static partial class StringExtensions
    {
        public static string? ToShortMarkdown(this string? source, 
            out string? displayed, int maxMarkdownLength = 80, bool byWords = false, 
            string? suffix = null, bool lengthWithSuffix = false)
        {
            if (string.IsNullOrWhiteSpace(source)) return displayed = source;
            if (maxMarkdownLength < 1) return displayed = null;

            var pipeline = new MarkdownPipelineBuilder().Build();
            var document = Markdown.Parse(source, pipeline);

            var len = document.ShortenMarkdown(maxMarkdownLength, byWords, suffix, lengthWithSuffix);

            if (len == 0) return displayed = null;

            var ms = new MemoryStream();
            using var writer = new StreamWriter(ms, leaveOpen: true);
            using var reader = new StreamReader(ms, leaveOpen: true);

            var renderer = new NormalizeRenderer(writer);
            pipeline.Setup(renderer);
            renderer.Write(document);
            writer.Flush();

            ms.Position = 0;
            var raw = reader.ReadToEnd();

            // Reset for second write
            ms.SetLength(0); // Clear the stream content
            ms.Position = 0;
            var displayRender = new HtmlRenderer(writer)
            {
                EnableHtmlForBlock = false,
                EnableHtmlForInline = false,
                EnableHtmlEscape = false,
            };
            pipeline.Setup(displayRender);
            displayRender.Render(document);
            writer.Flush();
            ms.SetLength(ms.Length - 1); // Remove the last byte

            ms.Position = 0;
            displayed = reader.ReadToEnd();

            return raw;
        }

        public static int ShortenMarkdown(this MarkdownObject node, int maxMarkedLen, 
            bool byWords, string? suffix = null, bool lengthWithSuffix = false)
        {
            var gotLen = 0;
            var sufLen = lengthWithSuffix ? suffix?.Length ?? 0 : 0;
            var remainLen = maxMarkedLen - sufLen;

            if (node is LiteralInline literal)
            {
                gotLen = Math.Max(0, Math.Min(literal.Content.Length, remainLen));
                var start = literal.Content.Start;
                var endIdx = start + gotLen - 1;

                if (remainLen == gotLen && gotLen > 0 && 
                    (gotLen < literal.Content.Length || literal.NextSibling is not null))
                {
                    if (byWords)
                    {
                        var lastAvailSpaceIdx = literal.Content.Text.LastIndexOf(' ', endIdx);
                        if (lastAvailSpaceIdx >= start)
                            gotLen = -1;
                        else
                            gotLen = 0;

                        endIdx = lastAvailSpaceIdx - 1;
                    }
                    else
                        gotLen = -1;
                }

                literal.Content.End = endIdx;
            }
            else if (node is ContainerInline inlinecont)
            {
                var preLoopLen = remainLen;
                var truncated = false;
                var contGotLen = 0;
                foreach (var inlineNode in inlinecont)
                {
                    if (remainLen < 1)
                    {
                        inlineNode.Remove();
                        continue;
                    }
                    var subLen = ShortenMarkdown(inlineNode, remainLen, byWords, suffix);
                    contGotLen += subLen;
                    if (subLen == 0)
                    {
                        inlineNode.Remove();
                        subLen = remainLen;
                    }
                    else if (subLen < 0)
                    {
                        contGotLen = preLoopLen;
                        truncated = true;
                        subLen = remainLen;
                    }
                    remainLen -= subLen;
                }
                if (contGotLen == 0)
                {
                    inlinecont.Remove();
                }
                else if (contGotLen > 0 && suffix is not null && truncated)
                {
                    var sufInline = new LiteralInline(suffix) { IsClosed = true };
                    inlinecont.LastChild!.InsertAfter(sufInline);
                }
                gotLen += contGotLen;
            }
            else if (node is ContainerBlock blockcont)
            {
                var contGotLen = 0;
                foreach (var subNode in blockcont)
                {
                    if (remainLen < 1)
                    {
                        blockcont.Remove(subNode);
                        continue;
                    }
                    var subLen = ShortenMarkdown(subNode, remainLen, byWords, suffix);
                    contGotLen += subLen;
                    if (subLen == 0)
                    {
                        blockcont.Remove(subNode);
                        subLen = remainLen;
                    }
                    remainLen -= subLen;
                }
                if (contGotLen == 0)
                {
                }
                gotLen += contGotLen;
            }
            else if (node is LeafBlock leaf && leaf.Inline != null)
            {
                gotLen = ShortenMarkdown(leaf.Inline, remainLen, byWords, suffix);
            }

            return gotLen;
        }

        public static string? FlattenNormalized(this string? source, int maxLength = -1, 
            bool byWords = false, string? suffix = null)
        {
            if (string.IsNullOrWhiteSpace(source)) return source;

            var suffixLen = suffix?.Length ?? 0;
            // If maxLength is < 0, use the full string length
            int targetLength = maxLength < 0 ? source.Length : maxLength;

            if (targetLength < 0) return string.Empty;

            Span<char> buffer = stackalloc char[targetLength + suffixLen];
            var span = source.AsSpan();
            var walked = 0;
            int written = 0;
            int lastSpaceIndex = -1;
            char lastc = '\0';
            foreach (char c in span)
            {
                walked++;

                if (written >= targetLength) break;
                if (c == '\r') continue;

                if (c != '\n' && c != '\t')
                {
                    if (c == ' ') 
                        lastSpaceIndex = written;
                    buffer[written++] = c;
                }
                else if (lastc != ' ')
                {
                    lastSpaceIndex = written;
                    buffer[written++] = ' ';
                    lastc = ' ';
                    continue;
                }
                else if (c == '\t') continue;

                lastc = c;
            }

            if (walked < source.Length)
            {
                if (byWords && lastSpaceIndex >= 0)
                    written = lastSpaceIndex;
                else if (byWords)
                    return string.Empty;
                else
                    written = Math.Max(0, written - suffixLen);

                if (suffix != null)
                {
                    foreach (var c in suffix.AsSpan())
                        buffer[written++] = c;
                }
            }

            if (buffer[written - 1] == ' ' && written > 0) written--;

            return buffer[..written].ToString();
        }
    }
}
