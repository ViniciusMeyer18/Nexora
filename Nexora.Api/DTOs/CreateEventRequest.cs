using System.ComponentModel.DataAnnotations;
using System.Text.Json;

namespace Nexora.Api.DTOs;

public class CreateEventRequest
{
    [Required]
    public string SystemName { get; set; } = string.Empty;

    [Required]
    public string EventId { get; set; } = string.Empty;

    [Required]
    public string RecordId { get; set; } = string.Empty;

    [Required]
    public string EventType { get; set; } = string.Empty;

    public DateTime OccurredAt { get; set; }

    public JsonElement Data { get; set; }
}