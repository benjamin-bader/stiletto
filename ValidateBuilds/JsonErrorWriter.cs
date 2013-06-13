using System.IO;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace ValidateBuilds
{
    public class JsonErrorWriter : IErrorWriter
    {
        private JsonSerializer serializer;
        private TextWriter writer;
        private bool disposed;

        public JsonErrorWriter(TextWriter writer)
        {
            serializer = new JsonSerializer();
            this.writer = writer;
        }

        public void Write(ValidationError error)
        {
            serializer.Serialize(writer, ToJson(error));
        }

        private static JObject ToJson(ValidationError error)
        {
            return new JObject(
                new JProperty("type", error.Type.ToString()),
                new JProperty("message", error.Message),
                new JProperty("project", error.ProjectFile.FullName));
        }

        public void Dispose()
        {
            if (disposed)
            {
                return;
            }

            if (writer != null)
            {
                writer.Close();
                writer = null;
            }

            serializer = null;

            disposed = true;
        }
    }
}
