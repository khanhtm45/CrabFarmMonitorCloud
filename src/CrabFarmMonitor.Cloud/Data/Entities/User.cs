namespace CrabFarmMonitor.Cloud.Data.Entities;

public class User
{
    public Guid Id { get; set; }
    public Guid OrgId { get; set; }
    public string? Username { get; set; }
    public string Email { get; set; } = "";
    public string? PasswordHash { get; set; }
    public string? FullName { get; set; }
    public string? DisplayName { get; set; }
    public string? Phone { get; set; }
    public string Role { get; set; } = "staff";
    public bool IsActive { get; set; } = true;
    public DateTime CreatedAt { get; set; }
}
