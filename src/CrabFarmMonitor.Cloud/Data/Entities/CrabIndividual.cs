namespace CrabFarmMonitor.Cloud.Data.Entities;

public class CrabIndividual
{
    public Guid Id { get; set; }
    public Guid BoxId { get; set; }
    public Guid FarmId { get; set; }
    public string TagCode { get; set; } = "";
    public string Status { get; set; } = "healthy";
    public int? WeightGrams { get; set; }
    public string? MoltStage { get; set; }
    public string? HealthNote { get; set; }
    public DateTime? LastWeighedAt { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }
    public CrabBox? Box { get; set; }
}
