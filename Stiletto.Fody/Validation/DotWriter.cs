using System;
using System.Collections.Generic;
using System.IO;
using System.Text.RegularExpressions;

namespace Stiletto.Fody.Validation
{
    public class DotWriter : IDisposable
    {
        private const string Indentation = "  ";

        private static readonly Regex DotIdExpression = new Regex(
            @"^[a-zA-Z_\200-\377][a-zA-Z0-9_\200-\377]*$",
            RegexOptions.Compiled);

        private int indentLevel;
        private int ids;
        private IDictionary<string, string> keyToNode;
        private StreamWriter writer;
        private bool disposed;

        public DotWriter(Stream stream)
        {
            keyToNode = new Dictionary<string, string>(StringComparer.Ordinal);
            writer = new StreamWriter(stream);
        }

        public void BeginGraph(params string[] attributes)
        {
            WriteIndentation();

            writer.Write(indentLevel == 0 ? "digraph " : "subgraph ");
            writer.Write(NextId(indentLevel == 0 ? "G" : "cluster"));
            writer.WriteLine(" {");

            Indent();

            WriteAttributes(attributes);
        }

        public void WriteNode(string node, params string[] attrs)
        {
            var name = NodeId(node);
            WriteIndentation();
            writer.Write(name);
            WriteInlineAttributes(attrs);
            writer.WriteLine(";");
        }

        public void WriteEdge(string source, string target, params string[] attrs)
        {
            var sourceName = NodeId(source);
            var targetName = NodeId(target);
            WriteIndentation();
            writer.Write(sourceName);
            writer.Write(" -> ");
            writer.Write(targetName);
            WriteInlineAttributes(attrs);
            writer.WriteLine(";");
        }

        public void EndGraph()
        {
            Conditions.Assert(indentLevel > 0, "Can't end a graph when indentLevel is 0!");
            Outdent();
            WriteIndentation();
            writer.WriteLine("}");
        }

        private void WriteIndentation()
        {
            for (var i = 0; i < indentLevel; ++i)
            {
                writer.Write(Indentation);
            }
        }

        private void WriteInlineAttributes(string[] attributes)
        {
            if (attributes.Length == 0) return;
            Conditions.Assert((attributes.Length & 1) == 0, "Invalid attributes (odd number)");

            writer.Write(" [");
            for (var i = 0; i < attributes.Length; i += 2)
            {
                writer.Write(attributes[i]);
                writer.Write("=");
                writer.Write("\"");
                writer.Write(attributes[i + 1]);
                writer.Write("\"");
            }
            writer.Write("]");
        }

        private void WriteAttributes(string[] attributes)
        {
            if (attributes.Length == 0) return;
            Conditions.Assert((attributes.Length & 1) == 0, "Invalid attributes (odd number)");

            for (var i = 0; i < attributes.Length; i += 2)
            {
                WriteIndentation();
                writer.Write(attributes[i]);
                writer.Write(" = \"");
                writer.Write(attributes[i + 1]);
                writer.WriteLine("\";");
            }
        }

        private void Indent()
        {
            ++indentLevel;
        }

        private void Outdent()
        {
            --indentLevel;
        }

        private string NodeId(string key)
        {
            if (DotIdExpression.IsMatch(key)) return key;

            string generatedId;
            if (keyToNode.TryGetValue(key, out generatedId))
            {
                return generatedId;
            }

            generatedId = NextId("N");
            keyToNode[key] = generatedId;
            WriteNode(generatedId, "label", key);
            return generatedId;
        }

        private string NextId(string name)
        {
            return name + (ids++);
        }

        public void Dispose()
        {
            if (disposed)
                return;

            if (writer != null)
            {
                writer.Flush();
                writer.Dispose();
                writer = null;
            }

            disposed = true;
        }
    }
}
