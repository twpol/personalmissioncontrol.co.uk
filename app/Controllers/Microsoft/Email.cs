using System.Threading.Tasks;
using app.Auth;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Graph;

namespace app.Controllers.Microsoft
{
    [Authorize("Microsoft")]
    [ApiController]
    [Route("api/microsoft/email")]
    [Produces("application/json")]
    public class EmailController : ControllerBase
    {
        [HttpGet("status")]
        public async Task<IActionResult> Status(string id, [FromServices] MicrosoftGraphProvider graphProvider)
        {
            if (!graphProvider.TryGet("Microsoft", out var client, out _)) return NotFound("Microsoft account not found");

            var message = await client.Me.Messages[id].Request()
                .Select(message => new
                {
                    message.IsRead,
                    message.Flag,
                })
                .GetAsync();
            return Ok(new MessageStatus(
                message.IsRead == false,
                message.Flag.FlagStatus == FollowupFlagStatus.Flagged,
                message.Flag.FlagStatus == FollowupFlagStatus.Complete
            ));
        }

        [HttpPost("status")]
        public async Task<IActionResult> Status(string id, bool? unread, bool? flagged, bool? completed, [FromServices] MicrosoftGraphProvider graphProvider)
        {
            if (!graphProvider.TryGet("Microsoft", out var client, out _)) return NotFound("Microsoft account not found");

            var message = new Message();
            if (unread.HasValue) message.IsRead = !unread.Value;
            if (flagged.HasValue || completed.HasValue) message.Flag = new FollowupFlag();
            if (flagged.HasValue) message.Flag.FlagStatus = flagged.Value ? FollowupFlagStatus.Flagged : FollowupFlagStatus.NotFlagged;
            if (completed.HasValue) message.Flag.FlagStatus = completed.Value ? FollowupFlagStatus.Complete : FollowupFlagStatus.Flagged;

            await client.Me.Messages[id].Request().UpdateAsync(message);
            return Ok();
        }

        public record MessageStatus(bool Unread, bool Flagged, bool Completed);
    }
}
