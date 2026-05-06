// Models/WorkOrder.cs
using System.Text.Json.Serialization;
using Newtonsoft.Json;

namespace RepairPlannerAgent.Models;

public sealed class WorkOrder
{
    [JsonPropertyName("id")]
    [JsonProperty("id")]
    public string Id { get; set; } = string.Empty;
    
    [JsonPropertyName("workOrderNumber")]
    [JsonProperty("workOrderNumber")]
    public string WorkOrderNumber { get; set; } = string.Empty;
    
    [JsonPropertyName("machineId")]
    [JsonProperty("machineId")]
    public string MachineId { get; set; } = string.Empty;
    
    [JsonPropertyName("title")]
    [JsonProperty("title")]
    public string Title { get; set; } = string.Empty;
    
    [JsonPropertyName("description")]
    [JsonProperty("description")]
    public string Description { get; set; } = string.Empty;
    
    [JsonPropertyName("type")]
    [JsonProperty("type")]
    public string Type { get; set; } = string.Empty;
    
    [JsonPropertyName("priority")]
    [JsonProperty("priority")]
    public string Priority { get; set; } = string.Empty;
    
    [JsonPropertyName("status")]
    [JsonProperty("status")]
    public string Status { get; set; } = string.Empty;
    
    [JsonPropertyName("assignedTo")]
    [JsonProperty("assignedTo")]
    public string? AssignedTo { get; set; }
    
    [JsonPropertyName("notes")]
    [JsonProperty("notes")]
    public string Notes { get; set; } = string.Empty;
    
    [JsonPropertyName("estimatedDuration")]
    [JsonProperty("estimatedDuration")]
    public int EstimatedDuration { get; set; }
    
    [JsonPropertyName("partsUsed")]
    [JsonProperty("partsUsed")]
    public IReadOnlyList<WorkOrderPartUsage> PartsUsed { get; set; } = Array.Empty<WorkOrderPartUsage>();
    
    [JsonPropertyName("tasks")]
    [JsonProperty("tasks")]
    public IReadOnlyList<RepairTask> Tasks { get; set; } = Array.Empty<RepairTask>();
    
    [JsonPropertyName("createdAt")]
    [JsonProperty("createdAt")]
    public DateTimeOffset CreatedAt { get; set; }
    
    [JsonPropertyName("updatedAt")]
    [JsonProperty("updatedAt")]
    public DateTimeOffset UpdatedAt { get; set; }
}
