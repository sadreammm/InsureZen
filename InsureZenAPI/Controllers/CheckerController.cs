using InsureZenAPI.Data;
using InsureZenAPI.Models;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

using UserModel = InsureZenAPI.Models.User;
using Decision = InsureZenAPI.Models.Review.Decision;

namespace InsureZenAPI.Controllers;

[ApiController]
[Route("api/[controller]")]
public class CheckerController : ControllerBase
{
    private readonly InsureZenContext context;
    public CheckerController(InsureZenContext context)
    {
        this.context = context;
    }

    [HttpGet("{claimId}/claim")]
    public async Task<ActionResult> GetClaimDetails([FromHeader(Name="X-User-Id")] Guid userId, Guid claimId)
    {
        var currentUser = await context.Users.FindAsync(userId);
        if (currentUser == null || currentUser.Role != UserModel.UserRole.Checker)
        {
            return Forbid();
        }

        var claim = await context.Claims.Include(c => c.Company).FirstOrDefaultAsync(c => c.ClaimId == claimId);
        if (claim == null)
        {
            return NotFound();
        }

        var canView = claim.Status == ClaimStatus.Checker_Pending ||
        (claim.Status == ClaimStatus.Checker_In_Progress && claim.ReviewerId == userId);

        if (!canView) 
        {
            return Forbid();
        }

        var review = await context.Reviews.FirstOrDefaultAsync(r => r.ClaimId == claimId);
        if (review == null)
        {
            return NotFound("Review not found for this claim.");
        }

        return Ok(new
        {
            claimId = claim.ClaimId,
            companyName = claim.Company?.CompanyName,
            normalizedClaimData = claim.NormalizedClaimData,
            status = claim.Status.ToString(),
            makerRecommendation = review.MakerRecommendation.ToString(),
            makerFeedback = review.MakerFeedback,
            makerSubmittedAt = review.MakerSubmittedAt
        });
    }

    [HttpGet("claims/pending")]
    public async Task<ActionResult> GetPendingClaims([FromHeader(Name="X-User-Id")] Guid userId)
    {
        var currentUser = await context.Users.FindAsync(userId);
        if (currentUser == null || currentUser.Role != UserModel.UserRole.Checker)
        {
            return Forbid();
        }

        var pendingClaims = await context.Claims
            .Where(c => c.Status == ClaimStatus.Checker_Pending)
            .Join(context.Reviews,
                claim => claim.ClaimId,
                review => review.ClaimId,
                (claim, review) => new
                {
                    reviewId = review.ReviewId,
                    claimId = claim.ClaimId,
                    companyId = claim.CompanyId,

                    makerRecommendation = review.MakerRecommendation.ToString(),
                    submittedAt = claim.SubmittedAt
                })
            .ToListAsync();

        return Ok(pendingClaims);
    }

    [HttpPost("{claimId}/accept")]
    public async Task<ActionResult> AcceptClaim([FromHeader(Name="X-User-Id")] Guid userId, Guid claimId)
    {
        var currentUser = await context.Users.FindAsync(userId);
        if (currentUser == null || currentUser.Role != UserModel.UserRole.Checker)
        {
            return Forbid();
        }

        var claim = await context.Claims
            .Include(c => c.Company)
            .FirstOrDefaultAsync(c => c.ClaimId == claimId);
        if (claim == null)
        {
            return NotFound();
        }
        if (claim.Status != ClaimStatus.Checker_Pending)
        {
            return BadRequest("Claim is not in a state that can be accepted.");
        }

        var review = await context.Reviews.FirstOrDefaultAsync(r => r.ClaimId == claimId);
        if (review == null)
        {
            return NotFound("Review not found.");
        }

        review.CheckerId = userId;

        var updated = await context.Claims
            .Where(c => c.ClaimId == claimId && c.Status == ClaimStatus.Checker_Pending)
            .ExecuteUpdateAsync(s => s
                .SetProperty(c => c.Status, ClaimStatus.Checker_In_Progress)
                .SetProperty(c => c.ReviewerId, currentUser.UserId)
            );
        
        if (updated == 0)
        {
            return BadRequest("Claim is not available");
        }

        return Ok(new
        {
            Message = "Claim accepted and is now in progress.",
            CheckerId = userId,
            ClaimId = claim.ClaimId,
            Status = claim.Status.ToString(),
            CheckerAcceptedAt = DateTime.UtcNow
        });
    }

    [HttpPost("{claimId}/submit")]
    public async Task<ActionResult> SubmitReview([FromHeader(Name="X-User-Id")] Guid userId, Guid claimId, [FromBody] Decision finalDecision)
    {
        var currentUser = await context.Users.FindAsync(userId);
        if (currentUser == null || currentUser.Role != UserModel.UserRole.Checker)
        {
            return Forbid();
        }

        var claim = await context.Claims.FindAsync(claimId);
        if (claim == null) 
        {
            return NotFound();
        }
        if (claim.Status != ClaimStatus.Checker_In_Progress)
        {
            return BadRequest("Claim is not in a state that can be reviewed.");
        }

        var review = await context.Reviews.FirstOrDefaultAsync(r => r.ClaimId == claimId);
        if (review == null)
        {
            return NotFound("Review not found for this claim.");
        }

        claim.Status = ClaimStatus.Completed;
        claim.CompletedAt = DateTime.UtcNow;
        claim.ReviewerId = userId;
        review.FinalDecision = finalDecision;
        review.CheckerSubmittedAt = DateTime.UtcNow;
        await context.SaveChangesAsync();

        var forwardMessage = ForwardClaim(claim);

        return Ok(new
        {
            Message = "Claim reviewed and completed. " + forwardMessage,
            CheckerId = userId,
            ClaimId = claim.ClaimId,
            ReviewId = review.ReviewId,
            FinalDecision = review.FinalDecision.ToString(),
            CheckerSubmittedAt = DateTime.UtcNow
        });
    }

    private string ForwardClaim(Claim claim)
    {
        var review = context.Reviews.FirstOrDefault(r => r.ClaimId == claim.ClaimId);
        if (review == null)
        {
            return "Review not found for this claim.";
        }
        // This should forward the completed claim and review details back to the insurance company
        // For simplicity, we are just returning a string here
        var forwardMessage = $"""
    Forwarding claim {claim.ClaimId} with final decision {review.FinalDecision} to company {claim.Company?.CompanyName}
    Claim Data: {claim.RawClaimData}
    Normalized Claim Data: {claim.NormalizedClaimData}
    """;
        return forwardMessage;
    }
}