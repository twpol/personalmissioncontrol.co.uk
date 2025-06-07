namespace app.Models
{
    public record EmailFolderModel(string AccountId, string ParentId, string ItemId, string Name, int TotalItemCount, int UnreadItemCount) : BaseModel(AccountId, ParentId, ItemId);
}