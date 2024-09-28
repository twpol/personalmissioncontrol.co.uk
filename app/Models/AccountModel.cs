using Microsoft.AspNetCore.Authentication;

namespace app.Models
{
    public record AccountModel(string AccountId, string ParentId, string ItemId, AuthenticationProperties AuthenticationProperties) : BaseModel(AccountId, ParentId, ItemId);
}
