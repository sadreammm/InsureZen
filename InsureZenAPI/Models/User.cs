namespace InsureZenAPI.Models;

public class User
{
    public Guid UserId { get; set; } = Guid.NewGuid();
    public string Name { get; set; } = string.Empty;
    public UserRole Role { get; set; } = UserRole.Employee;
    public enum UserRole
    {
        Employee,
        Maker,
        Checker
    }
}