// RepairPlannerAgent.cs
using System.Text.Json;
using System.Text.Json.Serialization;
using Azure.AI.Projects;
using Azure.AI.Projects.OpenAI;
using Microsoft.Agents.AI;
using Microsoft.Extensions.Logging;
using RepairPlannerAgent.Models;
using RepairPlannerAgent.Services;

namespace RepairPlannerAgent.Agents;

public sealed class RepairPlannerAgent(
    AIProjectClient projectClient,
    CosmosDbService cosmosDb,
    IFaultMappingService faultMapping,
    string modelDeploymentName,
    ILogger<RepairPlannerAgent> logger)
{
    private const string AgentName = "RepairPlannerAgent";
    
    private const string AgentInstructions = """
        You are a Repair Planner Agent for tire manufacturing equipment.
        Generate a repair plan with tasks, timeline, and resource allocation.
        Return the response as valid JSON matching the WorkOrder schema.
        
        Output JSON with these fields:
        - workOrderNumber, machineId, title, description
        - type: "corrective" | "preventive" | "emergency"
        - priority: "critical" | "high" | "medium" | "low"
        - status, assignedTo (technician id or null), notes
        - estimatedDuration: integer (minutes, e.g. 60 not "60 minutes")
        - partsUsed: [{ partId, partNumber, quantity }]
        - tasks: [{ sequence, title, description, estimatedDurationMinutes (integer), requiredSkills, safetyNotes }]
        
        IMPORTANT: All duration fields must be integers representing minutes (e.g. 90), not strings.
        
        Rules:
        - Assign the most qualified available technician
        - Include only relevant parts; empty array if none needed
        - Tasks must be ordered and actionable
        """;
    
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true,
        NumberHandling = JsonNumberHandling.AllowReadingFromString,
    };
    
    public async Task EnsureAgentVersionAsync(CancellationToken ct = default)
    {
        try
        {
            var definition = new PromptAgentDefinition(model: modelDeploymentName)
            {
                Instructions = AgentInstructions
            };
            
            await projectClient.Agents.CreateAgentVersionAsync(
                AgentName,
                new AgentVersionCreationOptions(definition),
                ct);
            
            logger.LogInformation("Agent version created/updated: {AgentName}", AgentName);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error ensuring agent version for {AgentName}", AgentName);
            throw;
        }
    }
    
    public async Task<WorkOrder> PlanAndCreateWorkOrderAsync(DiagnosedFault fault, CancellationToken ct = default)
    {
        try
        {
            logger.LogInformation("Planning repair for fault: {FaultType} on machine {MachineId}", 
                fault.FaultType, fault.MachineId);
            
            // Get required skills and parts from mapping
            var requiredSkills = faultMapping.GetRequiredSkills(fault.FaultType);
            var requiredPartNumbers = faultMapping.GetRequiredParts(fault.FaultType);
            
            logger.LogDebug("Required skills: {Skills}", string.Join(", ", requiredSkills));
            logger.LogDebug("Required parts: {Parts}", string.Join(", ", requiredPartNumbers));
            
            // Query available technicians
            var availableTechnicians = await cosmosDb.QueryTechniciansBySkillsAsync(requiredSkills, ct);
            logger.LogInformation("Found {Count} available technicians", availableTechnicians.Count);
            
            // Select most qualified technician (with most matching skills)
            var selectedTechnician = availableTechnicians
                .OrderByDescending(t => t.Skills.Count(s => requiredSkills.Contains(s)))
                .FirstOrDefault();
            
            // Query parts inventory
            var availableParts = requiredPartNumbers.Any() 
                ? await cosmosDb.FetchPartsByNumbersAsync(requiredPartNumbers, ct)
                : new List<Part>();
            
            logger.LogInformation("Found {Count} required parts in inventory", availableParts.Count);
            
            // Build prompt for agent
            var prompt = BuildRepairPlanningPrompt(fault, selectedTechnician, availableParts);
            
            WorkOrder workOrder;
            try
            {
                // Invoke agent via Foundry Agents SDK
                var aiAgent = projectClient.GetAIAgent(name: AgentName);
                var response = await aiAgent.RunAsync(prompt, thread: null, options: null, cancellationToken: ct);
                
                var agentResponse = response.Text ?? string.Empty;
                logger.LogDebug("Agent response: {Response}", agentResponse);
                
                // Parse agent response as WorkOrder
                workOrder = ParseWorkOrderFromResponse(agentResponse, fault);
            }
            catch (Exception ex)
            {
                logger.LogWarning(ex, "AI service unavailable, creating mock work order for demonstration");
                
                // Create mock work order when AI service is unavailable
                workOrder = CreateMockWorkOrder(fault, selectedTechnician, availableParts);
            }
            
            workOrder.AssignedTo = selectedTechnician?.Id;
            workOrder.PartsUsed = availableParts
                .Select(p => new WorkOrderPartUsage
                {
                    PartId = p.Id,
                    PartNumber = p.PartNumber,
                    Quantity = 1
                })
                .ToList();
            
            // Save work order to Cosmos DB
            await cosmosDb.CreateWorkOrderAsync(workOrder, ct);
            
            logger.LogInformation(
                "Work order created successfully: {WorkOrderNumber} assigned to {TechnicianName}",
                workOrder.WorkOrderNumber,
                selectedTechnician?.Name ?? "Unassigned");
            
            return workOrder;
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error planning repair for fault {FaultType}", fault.FaultType);
            throw;
        }
    }
    
    private string BuildRepairPlanningPrompt(DiagnosedFault fault, Technician? technician, IReadOnlyList<Part> parts)
    {
        var technicianInfo = technician != null
            ? $"Available Technician: {technician.Name} (ID: {technician.Id}) with skills: {string.Join(", ", technician.Skills)}"
            : "No available technicians found";
        
        var partsInfo = parts.Any()
            ? string.Join(", ", parts.Select(p => $"{p.PartNumber} (Qty: {p.QuantityAvailable})"))
            : "No parts required";
        
        return $"""
            Fault Details:
            - Type: {fault.FaultType}
            - Machine: {fault.MachineId}
            - Severity: {fault.Severity}
            - Description: {fault.Description}
            
            {technicianInfo}
            
            Available Parts: {partsInfo}
            
            Please create a comprehensive repair plan as a JSON WorkOrder.
            """;
    }
    
    private WorkOrder ParseWorkOrderFromResponse(string response, DiagnosedFault fault)
    {
        try
        {
            // Extract JSON from response (agent might include extra text)
            var jsonStart = response.IndexOf('{');
            var jsonEnd = response.LastIndexOf('}');
            
            if (jsonStart < 0 || jsonEnd < 0 || jsonEnd <= jsonStart)
            {
                throw new FormatException("No valid JSON found in agent response");
            }
            
            var jsonString = response.Substring(jsonStart, jsonEnd - jsonStart + 1);
            var workOrder = JsonSerializer.Deserialize<WorkOrder>(jsonString, JsonOptions)
                ?? throw new FormatException("Failed to deserialize WorkOrder from JSON");
            
            // Apply defaults and ensure required fields
            workOrder.Id ??= Guid.NewGuid().ToString();
            workOrder.WorkOrderNumber ??= $"WO-{DateTime.UtcNow:yyyyMMddHHmmss}";
            workOrder.MachineId = fault.MachineId;
            workOrder.Status ??= "pending";
            workOrder.Priority ??= "medium";
            workOrder.Type ??= "corrective";
            workOrder.EstimatedDuration = Math.Max(workOrder.EstimatedDuration, 0);
            
            logger.LogDebug("Parsed WorkOrder: {WorkOrderNumber}", workOrder.WorkOrderNumber);
            return workOrder;
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error parsing work order from agent response");
            throw;
        }
    }
    
    private WorkOrder CreateMockWorkOrder(DiagnosedFault fault, Technician? technician, IReadOnlyList<Part> parts)
    {
        var requiredSkills = faultMapping.GetRequiredSkills(fault.FaultType);
        
        var workOrder = new WorkOrder
        {
            WorkOrderNumber = $"WO-{DateTime.UtcNow:yyyyMMddHHmmss}",
            MachineId = fault.MachineId,
            Title = $"Repair for {fault.FaultType.Replace('_', ' ')}",
            Description = $"Automated repair plan for {fault.Description}",
            Type = "corrective",
            Priority = fault.Severity switch
            {
                "critical" => "critical",
                "high" => "high",
                _ => "medium"
            },
            Status = "pending",
            EstimatedDuration = 120, // 2 hours
            Tasks = new List<RepairTask>
            {
                new RepairTask
                {
                    Sequence = 1,
                    Title = "Initial inspection and diagnosis",
                    Description = "Inspect the equipment and confirm the fault diagnosis",
                    EstimatedDurationMinutes = 30,
                    RequiredSkills = ["general_maintenance"],
                    SafetyNotes = "Follow standard safety procedures"
                },
                new RepairTask
                {
                    Sequence = 2,
                    Title = "Perform repair work",
                    Description = "Execute the necessary repair procedures",
                    EstimatedDurationMinutes = 60,
                    RequiredSkills = requiredSkills.ToList(),
                    SafetyNotes = "Ensure equipment is properly locked out"
                },
                new RepairTask
                {
                    Sequence = 3,
                    Title = "Testing and verification",
                    Description = "Test the repair and verify equipment functionality",
                    EstimatedDurationMinutes = 30,
                    RequiredSkills = ["general_maintenance"],
                    SafetyNotes = "Test in safe mode only"
                }
            }
        };
        
        return workOrder;
    }
}