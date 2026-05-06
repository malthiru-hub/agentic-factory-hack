// Services/CosmosDbService.cs
using Microsoft.Azure.Cosmos;
using Microsoft.Extensions.Logging;
using RepairPlannerAgent.Models;
using RepairPlannerAgent.Services;

namespace RepairPlannerAgent.Services;

public sealed class CosmosDbService
{
    private readonly CosmosClient _cosmosClient;
    private readonly string _databaseName;
    private readonly ILogger<CosmosDbService> _logger;
    
    private const string TechniciansContainer = "Technicians";
    private const string PartsContainer = "PartsInventory";
    private const string WorkOrdersContainer = "WorkOrders";
    
    public CosmosDbService(CosmosClient cosmosClient, CosmosDbOptions options, ILogger<CosmosDbService> logger)
    {
        _cosmosClient = cosmosClient;
        _databaseName = options.DatabaseName;
        _logger = logger;
    }
    
    public async Task<IReadOnlyList<Technician>> QueryTechniciansBySkillsAsync(IReadOnlyList<string> requiredSkills, CancellationToken ct = default)
    {
        try
        {
            var container = _cosmosClient.GetContainer(_databaseName, TechniciansContainer);
            var query = new QueryDefinition(
                "SELECT * FROM c WHERE c.isAvailable = true AND ARRAY_CONTAINS(c.skills, @skill)")
                .WithParameter("@skill", requiredSkills.First()); // Simplified: check for at least one matching skill
            
            var iterator = container.GetItemQueryIterator<Technician>(query);
            var results = new List<Technician>();
            
            while (iterator.HasMoreResults)
            {
                var response = await iterator.ReadNextAsync(ct);
                results.AddRange(response.Where(t => requiredSkills.Any(skill => t.Skills.Contains(skill))));
            }
            
            _logger.LogInformation("Queried {Count} technicians matching skills: {Skills}", results.Count, string.Join(", ", requiredSkills));
            return results;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error querying technicians by skills: {Skills}", string.Join(", ", requiredSkills));
            throw;
        }
    }
    
    public async Task<IReadOnlyList<Part>> FetchPartsByNumbersAsync(IReadOnlyList<string> partNumbers, CancellationToken ct = default)
    {
        try
        {
            var container = _cosmosClient.GetContainer(_databaseName, PartsContainer);
            var results = new List<Part>();
            
            foreach (var partNumber in partNumbers)
            {
                var query = new QueryDefinition("SELECT * FROM c WHERE c.partNumber = @partNumber")
                    .WithParameter("@partNumber", partNumber);
                
                var iterator = container.GetItemQueryIterator<Part>(query);
                
                while (iterator.HasMoreResults)
                {
                    var response = await iterator.ReadNextAsync(ct);
                    results.AddRange(response);
                }
            }
            
            _logger.LogInformation("Fetched {Count} parts for numbers: {PartNumbers}", results.Count, string.Join(", ", partNumbers));
            return results;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error fetching parts by numbers: {PartNumbers}", string.Join(", ", partNumbers));
            throw;
        }
    }
    
    public async Task CreateWorkOrderAsync(WorkOrder workOrder, CancellationToken ct = default)
    {
        try
        {
            var container = _cosmosClient.GetContainer(_databaseName, WorkOrdersContainer);
            workOrder.Id = Guid.NewGuid().ToString();
            workOrder.CreatedAt = DateTimeOffset.UtcNow;
            workOrder.UpdatedAt = DateTimeOffset.UtcNow;
            
            await container.CreateItemAsync(workOrder, new PartitionKey(workOrder.Status), cancellationToken: ct);
            _logger.LogInformation("Created work order {WorkOrderNumber}", workOrder.WorkOrderNumber);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error creating work order {WorkOrderNumber}", workOrder.WorkOrderNumber);
            throw;
        }
    }
}
