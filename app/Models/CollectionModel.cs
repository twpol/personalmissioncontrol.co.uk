using System;

namespace app.Models
{
    public record CollectionModel(string AccountId, string ParentId, DateTimeOffset Change) : BaseModel(AccountId, ParentId, "");
}
