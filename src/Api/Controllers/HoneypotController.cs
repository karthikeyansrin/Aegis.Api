using System.Text.Json;
using Aegis.Application.Engines;
using Microsoft.AspNetCore.Mvc;

namespace Aegis.Api.Controllers;

[ApiController]
[Route("api/aegis")]
[Produces("application/json")]
public class HoneypotController : ControllerBase
{
    private readonly IConversationEngine _engine;

    public HoneypotController(IConversationEngine engine)
    {
        _engine = engine;
    }

    [HttpPost("analyze")]
    [Consumes("application/json")]
    public async Task<IActionResult> Analyze(
        [FromBody] JsonElement body,
        CancellationToken cancellationToken = default)
    {
        string sessionId = "default-session";
        string messageText = string.Empty;

        if (body.TryGetProperty("sessionId", out var sidElem) &&
            sidElem.ValueKind == JsonValueKind.String)
        {
            sessionId = sidElem.GetString() ?? sessionId;
        }
        else if (body.TryGetProperty("session_id", out var sessionIdElem) &&
                 sessionIdElem.ValueKind == JsonValueKind.String)
        {
            sessionId = sessionIdElem.GetString() ?? sessionId;
        }

        if (body.TryGetProperty("message", out var msgElem))
        {
            if (msgElem.ValueKind == JsonValueKind.Object &&
                msgElem.TryGetProperty("text", out var textElem))
            {
                messageText = textElem.GetString() ?? string.Empty;
            }
            else if (msgElem.ValueKind == JsonValueKind.String)
            {
                messageText = msgElem.GetString() ?? string.Empty;
            }
        }

        if (string.IsNullOrWhiteSpace(messageText) &&
            body.TryGetProperty("text", out var rawTextElem) &&
            rawTextElem.ValueKind == JsonValueKind.String)
        {
            messageText = rawTextElem.GetString() ?? string.Empty;
        }

        var result = await _engine.ProcessMessageAsync(sessionId, messageText, cancellationToken);
        
        return Ok(new
        {
            isScam = result.IsScam,
            scamType = result.ScamType,
            confidence = result.Confidence,
            agentReply = result.AgentReply,
            extractedIntelligence = new
            {
                upiIds = result.ExtractedIntelligence.UpiIds,
                phoneNumbers = result.ExtractedIntelligence.PhoneNumbers,
                urls = result.ExtractedIntelligence.Urls,
                bankAccounts = result.ExtractedIntelligence.BankAccounts
            }
        });
    }
}