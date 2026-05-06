// Program.cs
using Azure.AI.Projects;
using Azure.Identity;
using Microsoft.Azure.Cosmos;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using RepairPlannerAgent.Models;
using RepairPlannerAgent.Services;
using RepairPlannerAgent.Agents;

var services = new ServiceCollection();

// Configure Cosmos DB options from environment variables (with defaults for development)
var cosmosOptions = new CosmosDbOptions
{
    Endpoint = Environment.GetEnvironmentVariable("COSMOS_ENDPOINT") ?? "https://localhost:8081",
    Key = Environment.GetEnvironmentVariable("COSMOS_KEY") ?? "dGVzdC1jb3Ntb3Mta2V5LWZvci1kZXZlbG9wbWVudC1wdXJwb3Nlcy1vbmx5", // Valid Base64 string
    DatabaseName = Environment.GetEnvironmentVariable("COSMOS_DATABASE_NAME") ?? "RepairPlannerDB"
};

// Register services
services.AddSingleton(cosmosOptions);
services.AddSingleton(new CosmosClient(cosmosOptions.Endpoint, cosmosOptions.Key));
services.AddSingleton<IFaultMappingService, FaultMappingService>();
services.AddSingleton<CosmosDbService>();
services.AddLogging(builder => builder.AddConsole());

// Configure AI Project Client from environment variables (with defaults for development)
var aiProjectEndpoint = Environment.GetEnvironmentVariable("AZURE_AI_PROJECT_ENDPOINT") ?? "https://mock-ai-project.openai.azure.com";
var modelDeploymentName = Environment.GetEnvironmentVariable("MODEL_DEPLOYMENT_NAME") ?? "gpt-4o";

// Create AI Project Client with error handling
AIProjectClient? projectClient = null;
try
{
    projectClient = new AIProjectClient(new Uri(aiProjectEndpoint), new DefaultAzureCredential());
}
catch (Exception ex)
{
    Console.WriteLine($"Warning: Failed to create AI Project Client. Using mock mode. {ex.Message}");
}

services.AddSingleton(projectClient ?? new AIProjectClient(new Uri("https://mock.invalid"), new DefaultAzureCredential()));

// Register RepairPlannerAgent
services.AddSingleton<RepairPlannerAgent.Agents.RepairPlannerAgent>(sp =>
    new RepairPlannerAgent.Agents.RepairPlannerAgent(
        sp.GetRequiredService<AIProjectClient>(),
        sp.GetRequiredService<CosmosDbService>(),
        sp.GetRequiredService<IFaultMappingService>(),
        modelDeploymentName,
        sp.GetRequiredService<ILogger<RepairPlannerAgent.Agents.RepairPlannerAgent>>()));

await using var provider = services.BuildServiceProvider();
var agent = provider.GetRequiredService<RepairPlannerAgent.Agents.RepairPlannerAgent>();
var logger = provider.GetRequiredService<ILogger<Program>>();

try
{
    // Ensure agent version is created
    await agent.EnsureAgentVersionAsync();
    logger.LogInformation("Agent version registered successfully.");
}
catch (Exception ex)
{
    logger.LogWarning(ex, "Failed to register agent with Azure AI Foundry. Using mock mode for demonstration.");
}

// Create a sample diagnosed fault
var sampleFault = new DiagnosedFault
{
    Id = Guid.NewGuid().ToString(),
    MachineId = "TIRE-EXTRUDER-001",
    FaultType = "extruder_barrel_overheating",
    Description = "Extruder barrel temperature exceeding safe limits",
    Severity = "high",
    Timestamp = DateTimeOffset.UtcNow
};

logger.LogInformation("Starting repair planning for fault: {FaultType}", sampleFault.FaultType);

try
{
    var workOrder = await agent.PlanAndCreateWorkOrderAsync(sampleFault);
    logger.LogInformation("Work order created: {WorkOrderNumber} for machine {MachineId}", workOrder.WorkOrderNumber, workOrder.MachineId);
}
catch (Exception ex)
{
    logger.LogError(ex, "Error during repair planning workflow");
}

logger.LogInformation("Repair planning workflow completed.");
