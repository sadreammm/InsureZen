namespace InsureZenAPI.Models;

public class Review
{
    public Guid ReviewId { get; set; } = Guid.NewGuid();
    public Guid ClaimId { get; set; }
    public virtual Claim? Claim { get; set; }
    public Guid MakerId { get; set; }
    public String? MakerFeedback { get; set; }
    public Recommendation MakerRecommendation { get; set; }
    public DateTime MakerSubmittedAt { get; set; } = DateTime.UtcNow;
    public Guid? CheckerId { get; set; }
    public Decision? FinalDecision { get; set; }
    public DateTime? CheckerSubmittedAt { get; set; }


    public enum Recommendation
    {
        Approve,
        Reject
    }

    public enum Decision
    {
        Approved,
        Rejected
    }
}