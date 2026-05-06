// Models/WorkOrderPartUsage.cs
using System.Text.Json.Serialization;
using Newtonsoft.Json;

namespace RepairPlannerAgent.Models;

public sealed class WorkOrderPartUsage
{
    [JsonPropertyName("partId")]
    [JsonProperty("partId")]
    public string PartId { get; set; } = string.Empty;
    
    [JsonPropertyName("partNumber")]
    [JsonProperty("partNumber")]
    public string PartNumber { get; set; } = string.Empty;
    
    [JsonPropertyName("quantity")]
    [JsonProperty("quantity")]
    public int Quantity { get; set; }
}