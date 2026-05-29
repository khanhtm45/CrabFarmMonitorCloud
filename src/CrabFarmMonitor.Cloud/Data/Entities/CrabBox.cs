namespace CrabFarmMonitor.Cloud.Data.Entities;

public class CrabBox
{
    public Guid Id { get; set; }
    public Guid FarmId { get; set; }
    public string Code { get; set; } = "";
    public string? Label { get; set; }
    public string? RowLabel { get; set; }
    public string Status { get; set; } = "active";
    public int Capacity { get; set; } = 1;
    public string? Notes { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }
    public List<CrabIndividual> Crabs { get; set; } = new();
}
