using System;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace app.Models.Api;

public class JsonConverterTask : JsonConverter<TaskModel>
{
    public override TaskModel? Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        throw new NotImplementedException();
    }

    public override void Write(Utf8JsonWriter writer, TaskModel value, JsonSerializerOptions options)
    {
        writer.WriteStartObject();
        writer.WriteString("id", value.Id);
        writer.WriteString("name", value.Name);
        writer.WriteBoolean("important", value.Important);
        writer.WriteString("created", value.Created);
        if (value.Completed.HasValue) writer.WriteString("completed", value.Completed.Value); else writer.WriteNull("completed");
        writer.WriteEndObject();
    }
}
