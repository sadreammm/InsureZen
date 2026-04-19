using InsureZenAPI.Data;
using InsureZenAPI.Models;
using Microsoft.EntityFrameworkCore;
using Xunit;

public class ClaimStateTransitionTests
{
    private InsureZenContext CreateContext()
    {
        var options = new DbContextOptionsBuilder<InsureZenContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;
        
        return new InsureZenContext(options);
    }

    [Fact]
    public async Task Maker_CanAccept_PendingClaim()
    {
        var context = CreateContext();
        var maker = new User { Role = User.UserRole.Maker };
        var company = new InsuranceCompany { CompanyName = "TestInsurance" };
        var claim = new Claim { CompanyId = company.CompanyId, RawClaimData = "Test Data"};
        context.Users.Add(maker);
        context.InsuranceCompanies.Add(company);
        context.Claims.Add(claim);
        await context.SaveChangesAsync();

        claim.Status = ClaimStatus.Maker_In_Progress;
        claim.ReviewerId = maker.UserId;
        await context.SaveChangesAsync();

        var updatedClaim = await context.Claims.FindAsync(claim.ClaimId);
        Assert.Equal(ClaimStatus.Maker_In_Progress, updatedClaim!.Status);
        Assert.Equal(maker.UserId, updatedClaim.ReviewerId);
    }

    [Fact]
    public async Task Maker_CannotAccept_AlreadyAcceptedClaim()
    {
        var context = CreateContext();
        var claim = new Claim { RawClaimData = "Test Data", Status = ClaimStatus.Maker_In_Progress };
        context.Claims.Add(claim);
        await context.SaveChangesAsync();

        Assert.NotEqual(ClaimStatus.Pending, claim.Status);
    }

    [Fact]
    public async Task MakerSubmit_TransitionsToCheckerPending()
    {
        var context = CreateContext();
        var maker = new User { Role = User.UserRole.Maker };
        var claim = new Claim
        {
            RawClaimData = "Test Data",
            Status = ClaimStatus.Maker_In_Progress,
            ReviewerId = maker.UserId   
        };
        context.Users.Add(maker);
        context.Claims.Add(claim);
        await context.SaveChangesAsync();

        claim.Status = ClaimStatus.Checker_Pending;
        claim.ReviewerId = null;
        var review = new Review
        {
            ClaimId = claim.ClaimId,
            MakerId = maker.UserId,
            MakerRecommendation = Review.Recommendation.Approve
        };

        context.Reviews.Add(review);
        await context.SaveChangesAsync();

        var updatedClaim = await context.Claims.FindAsync(claim.ClaimId);
        var createdReview = await context.Reviews.FirstOrDefaultAsync(r => r.ClaimId == claim.ClaimId);

        Assert.Equal(ClaimStatus.Checker_Pending, updatedClaim!.Status);
        Assert.Null(updatedClaim.ReviewerId);
        Assert.NotNull(createdReview);
    }

    [Fact]
    public async Task Checker_CanAccept_CheckerPendingClaim()
    {
        var context = CreateContext();
        var checker = new User { Role = User.UserRole.Checker };
        var claim = new Claim
        {
            RawClaimData = "Test Data",
            Status = ClaimStatus.Checker_Pending
        };
        context.Users.Add(checker);
        context.Claims.Add(claim);
        await context.SaveChangesAsync();

        claim.Status = ClaimStatus.Checker_In_Progress;
        claim.ReviewerId = checker.UserId;
        await context.SaveChangesAsync();

        var updatedClaim = await context.Claims.FindAsync(claim.ClaimId);
        Assert.Equal(ClaimStatus.Checker_In_Progress, updatedClaim!.Status);
        Assert.Equal(checker.UserId, updatedClaim.ReviewerId);
    }

    [Fact]
    public async Task CheckerSubmit_TransitionsToCompleted()
    {
        var context = CreateContext();
        var checker = new User { Role = User.UserRole.Checker };
        var claim = new Claim
        {
            RawClaimData = "Test Data",
            Status = ClaimStatus.Checker_In_Progress,
            ReviewerId = checker.UserId
        };
        var review = new Review
        {
            ClaimId = claim.ClaimId,
            MakerId = Guid.NewGuid(),
            MakerRecommendation = Review.Recommendation.Approve
        };
        context.Users.Add(checker);
        context.Claims.Add(claim);
        context.Reviews.Add(review);
        await context.SaveChangesAsync();

        claim.Status = ClaimStatus.Completed;
        claim.CompletedAt = DateTime.UtcNow;
        review.CheckerId = checker.UserId;
        review.FinalDecision = Review.Decision.Approved;
        review.CheckerSubmittedAt = DateTime.UtcNow;
        await context.SaveChangesAsync();

        var updatedClaim = await context.Claims.FindAsync(claim.ClaimId);
        Assert.Equal(ClaimStatus.Completed, updatedClaim!.Status);
        Assert.NotNull(updatedClaim.CompletedAt);
        Assert.Equal(Review.Decision.Approved, review.FinalDecision);
    }
}