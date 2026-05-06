// Models/Technician.cs
using System.Text.Json.Serialization;
using Newtonsoft.Json;

namespace RepairPlannerAgent.Models;

public sealed class Technician
{
    [JsonPropertyName("id")]
    [JsonProperty("id")]
    public string Id { get; set; } = string.Empty;
    
    [JsonPropertyName("name")]
    [JsonProperty("name")]
    public string Name { get; set; } = string.Empty;
    
    [JsonPropertyName("department")]
    [JsonProperty("department")]
    public string Department { get; set; } = string.Empty;
    
    [JsonPropertyName("skills")]
    [JsonProperty("skills")]
    public IReadOnlyList<string> Skills { get; set; } = Array.Empty<string>();
    
    [JsonPropertyName("isAvailable")]
    [JsonProperty("isAvailable")]
    public bool IsAvailable { get; set; }
    
    [JsonPropertyName("shiftStart")]
    [JsonProperty("shiftStart")]
    public TimeSpan ShiftStart { get; set; }
    
    [JsonPropertyName("shiftEnd")]
    [JsonProperty("shiftEnd")]
    public TimeSpan ShiftEnd { get; set; }
}