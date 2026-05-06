// Services/CosmosDbOptions.cs
namespace RepairPlannerAgent.Services;

public sealed class CosmosDbOptions
{
    public string Endpoint { get; set; } = string.Empty;
    public string Key { get; set; } = string.Empty;
    public string DatabaseName { get; set; } = string.Empty;
}