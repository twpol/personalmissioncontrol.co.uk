using System;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace app.Models.Api;

public class JsonConverterTaskList : JsonConverter<TaskListModel>
{
    public override TaskListModel? Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        throw new NotImplementedException();
    }

    public override void Write(Utf8JsonWriter writer, TaskListModel value, JsonSerializerOptions options)
    {
        writer.WriteStartObject();
        writer.WriteString("id", value.Id);
        writer.WriteString("emoji", value.Emoji);
        writer.WriteString("name", value.Name);
        writer.WriteEndObject();
    }
}
