using System;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace app.Models.Api;

public class JsonConverterEmailFolder : JsonConverter<EmailFolderModel>
{
    public override EmailFolderModel? Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        throw new NotImplementedException();
    }

    public override void Write(Utf8JsonWriter writer, EmailFolderModel value, JsonSerializerOptions options)
    {
        writer.WriteStartObject();
        writer.WriteString("id", value.Id);
        writer.WriteString("name", value.Name);
        writer.WriteNumber("total", value.TotalItemCount);
        writer.WriteNumber("unread", value.UnreadItemCount);
        writer.WriteEndObject();
    }
}
