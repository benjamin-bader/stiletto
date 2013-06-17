using System.IO;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace ValidateBuilds
{
    public class JsonErrorWriter : TextErrorWriter
    {
        private readonly JsonSerializer serializer;

        public JsonErrorWriter(TextWriter writer)
            : base(writer)
        {
            serializer = new JsonSerializer();
        }

        public override void Write(ValidationError error)
        {
            serializer.Serialize(Writer, ToJson(error));
        }

        private static JObject ToJson(ValidationError error)
        {
            return new JObject(
                new JProperty("type", error.Type.ToString()),
                new JProperty("message", error.Message),
                new JProperty("project", error.ProjectFile.FullName));
        }
    }
}
