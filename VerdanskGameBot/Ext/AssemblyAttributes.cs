using System;
using Jering.Javascript.NodeJS;

namespace VerdanskGameBot.Ext
{
    [AttributeUsage(AttributeTargets.Assembly, Inherited = false)]
    internal class AssemblyNodeJSAttribute(string path) : Attribute
    {
        /// <summary>
        /// To be used by <seealso cref="NodeJSProcessOptions.ExecutablePath"/>.
        /// </summary>
        public string Path { get; } = path;
    }
}
