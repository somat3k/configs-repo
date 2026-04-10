using Microsoft.AspNetCore.Mvc;

namespace MLS.Network.SubscriptionManager.Controllers;

/// <summary>Request body for topic subscriptions.</summary>
public sealed record SubscribeRequest(string topic, string connection_id);

/// <summary>Request body for publishing messages.</summary>
public sealed record PublishRequest(string topic, string message);

/// <summary>REST controller for subscription management endpoints.</summary>
[ApiController]
[Route("api/subscriptions")]
public sealed class SubscriptionManagerController(ISubscriptionService _service) : ControllerBase
{
    /// <summary>Returns all known topic names.</summary>
    [HttpGet("topics")]
    public IActionResult GetTopics() => Ok(_service.GetTopics());

    /// <summary>Streams all subscriptions for a given topic.</summary>
    [HttpGet("{topic}")]
    public async Task<IActionResult> GetSubscriptions(string topic)
    {
        var subs = new List<SubscriptionInfo>();
        await foreach (var s in _service.GetSubscriptionsAsync(topic, HttpContext.RequestAborted))
            subs.Add(s);
        return Ok(subs);
    }

    /// <summary>Creates a new subscription.</summary>
    [HttpPost]
    public async Task<IActionResult> Subscribe([FromBody] SubscribeRequest request)
    {
        var id = await _service.SubscribeAsync(
            request.topic, request.connection_id, HttpContext.RequestAborted).ConfigureAwait(false);
        return Ok(new { subscription_id = id });
    }

    /// <summary>Removes a subscription by ID.</summary>
    [HttpDelete("{id}")]
    public async Task<IActionResult> Unsubscribe(string id)
    {
        await _service.UnsubscribeAsync(id, HttpContext.RequestAborted).ConfigureAwait(false);
        return NoContent();
    }

    /// <summary>Publishes a message to all subscribers of a topic.</summary>
    [HttpPost("publish")]
    public async Task<IActionResult> Publish([FromBody] PublishRequest request)
    {
        var count = await _service.PublishAsync(
            request.topic, request.message, HttpContext.RequestAborted).ConfigureAwait(false);
        return Ok(new { delivered_to = count });
    }
}
