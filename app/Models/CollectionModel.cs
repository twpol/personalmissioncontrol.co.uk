namespace app.Models
{
    public record CollectionModel(string AccountId, string ParentId, string Change) : BaseModel(AccountId, ParentId, "");
}
