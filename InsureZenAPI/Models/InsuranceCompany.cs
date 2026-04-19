namespace InsureZenAPI.Models;

using System.ComponentModel.DataAnnotations;

public class InsuranceCompany
{
    [Key]
    public Guid CompanyId { get; set; } = Guid.NewGuid();
    public string CompanyName { get; set; } = string.Empty;
}