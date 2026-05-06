// Models/Part.cs
using System.Text.Json.Serialization;
using Newtonsoft.Json;

namespace RepairPlannerAgent.Models;

public sealed class Part
{
    [JsonPropertyName("id")]
    [JsonProperty("id")]
    public string Id { get; set; } = string.Empty;
    
    [JsonPropertyName("partNumber")]
    [JsonProperty("partNumber")]
    public string PartNumber { get; set; } = string.Empty;
    
    [JsonPropertyName("description")]
    [JsonProperty("description")]
    public string Description { get; set; } = string.Empty;
    
    [JsonPropertyName("category")]
    [JsonProperty("category")]
    public string Category { get; set; } = string.Empty;
    
    [JsonPropertyName("quantityAvailable")]
    [JsonProperty("quantityAvailable")]
    public int QuantityAvailable { get; set; }
    
    [JsonPropertyName("unitCost")]
    [JsonProperty("unitCost")]
    public decimal UnitCost { get; set; }
    
    [JsonPropertyName("supplierId")]
    [JsonProperty("supplierId")]
    public string SupplierId { get; set; } = string.Empty;
}