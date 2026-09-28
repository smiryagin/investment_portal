using System.Text.Json;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using WiseLine.Portal.Application.Email;

namespace WiseLine.Portal.Api.Controllers;

[ApiController]
[AllowAnonymous]
[IgnoreAntiforgeryToken]
[Route("api/webhooks/resend")]
public sealed class ResendWebhooksController(
    IEmailWebhookSignatureVerifier signatureVerifier,
    IEmailWebhookProcessor webhookProcessor) : ControllerBase
{
    [HttpPost]
    [RequestSizeLimit(256_000)]
    public async Task<IActionResult> Receive(CancellationToken cancellationToken)
    {
        var messageId = Request.Headers["svix-id"].ToString();
        var timestamp = Request.Headers["svix-timestamp"].ToString();
        var signature = Request.Headers["svix-signature"].ToString();

        using var reader = new StreamReader(Request.Body);
        var payload = await reader.ReadToEndAsync(cancellationToken);
        if (!signatureVerifier.IsValid(payload, messageId, timestamp, signature))
        {
            return Unauthorized();
        }

        try
        {
            await webhookProcessor.ProcessAsync(messageId, payload, cancellationToken);
            return Ok();
        }
        catch (JsonException)
        {
            return BadRequest();
        }
    }
}
