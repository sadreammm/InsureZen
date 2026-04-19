namespace InsureZenAPI.Models;

using Microsoft.EntityFrameworkCore;

[Index(nameof(Status))]
[Index(nameof(CompanyId))]
[Index(nameof(SubmittedAt))]
[Index(nameof(CompanyId), nameof(SubmittedAt))]
public class Claim
{
    public Guid ClaimId { get; set; } = Guid.NewGuid();
    public Guid CompanyId { get; set; }
    public virtual InsuranceCompany? Company { get; set; }
    public ClaimStatus Status { get; set; } = ClaimStatus.Pending;
    public String RawClaimData { get; set; } = string.Empty; // Claim Stored as String for simplicity, can be JSON or XML
    public String? NormalizedClaimData { get; set; } // Stored as String for simplicity, can implement company-specific normalization classes 
    public Guid? ReviewerId { get; set; }
    public virtual User? Reviewer { get; set; }
    public DateTime SubmittedAt { get; set; } = DateTime.UtcNow;
    public DateTime? CompletedAt { get; set; }
}

public enum ClaimStatus
{
    Pending, // Pending acceptance by maker
    Maker_In_Progress, // Accepted by maker and being worked on
    Checker_Pending, // Submitted by maker and pending checker review
    Checker_In_Progress, // Being reviewed by checker
    Completed // Final decision made by checker
}
