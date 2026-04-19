using InsureZenAPI.Data;
using InsureZenAPI.Models;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using UserModel = InsureZenAPI.Models.User;
using MakerRecommendation = InsureZenAPI.Models.Review.Recommendation;
using System.ComponentModel.DataAnnotations;

namespace InsureZenAPI.Controllers;


[ApiController]
[Route("api/[controller]")]
public class MakerController : ControllerBase
{
    private readonly InsureZenContext context;

    public MakerController(InsureZenContext context)
    {
        this.context = context;
    }

    [HttpGet("{claim_id}/claim")]
    public async Task<ActionResult> GetClaimDetails([FromHeader(Name = "X-User-Id")] Guid userId, Guid claim_id)
    {
        var currentUser = await context.Users.FindAsync(userId);
        if (currentUser == null || currentUser.Role != UserModel.UserRole.Maker)
        {
            return Forbid();
        }

        var claim = await context.Claims.Include(c => c.Company).FirstOrDefaultAsync(c => c.ClaimId == claim_id);
        if (claim == null)
        {
            return NotFound();
        }

        var canView = claim.Status == ClaimStatus.Checker_Pending ||
        (claim.Status == ClaimStatus.Maker_In_Progress && claim.ReviewerId == userId);

        if (!canView) 
        {
            return Forbid();
        }

        return Ok(new
        {
            claimId = claim.ClaimId,
            companyName = claim.Company?.CompanyName,
            normalizedClaimData = claim.NormalizedClaimData,
            status = claim.Status.ToString(),
        });
    }

    [HttpGet("claims/pending")]
    public async Task<ActionResult> GetPendingClaims([FromHeader(Name="X-User-Id")] Guid userId)
    {
        var currentUser = await context.Users.FindAsync(userId);
        if (currentUser == null || currentUser.Role != UserModel.UserRole.Maker)
        {
            return Forbid();
        }

        var pendingClaims = await context.Claims
            .Where(c => c.Status == ClaimStatus.Pending)
            .Select(c => new
            {
                claimId = c.ClaimId,
                companyId = c.CompanyId,
                submittedAt = c.SubmittedAt,
                status = c.Status.ToString()
            })
            .ToListAsync();

        return Ok(pendingClaims);
    }

    [HttpPost("{claimId}/accept")]
    public async Task<ActionResult> AcceptClaim([FromHeader(Name="X-User-Id")] Guid userId, Guid claimId)
    {
        var currentUser = await context.Users.FindAsync(userId);
        if (currentUser == null || currentUser.Role != UserModel.UserRole.Maker)
        {
            return Forbid();
        }
        var claim = await context.Claims.FindAsync(claimId);
        if (claim == null)
        {
            return NotFound();
        }
        if (claim.Status != ClaimStatus.Pending)
        {
            return BadRequest("Claim is not in a state that can be accepted.");
        }

        var updated = await context.Claims
            .Where(c => c.ClaimId == claimId && c.Status == ClaimStatus.Pending)
            .ExecuteUpdateAsync(s => s
                .SetProperty(c => c.Status, ClaimStatus.Maker_In_Progress)
                .SetProperty(c => c.ReviewerId, currentUser.UserId)
            );
        
        if (updated == 0)
        {
            return BadRequest("Claim is not available");
        }

        return Ok(new
        {
            Message = "Claim accepted and is now in progress.",
            MakerId = userId,
            ClaimId = claim.ClaimId,
            Status = claim.Status.ToString(),
            MakerAcceptedAt = DateTime.UtcNow
        });
    }

    public class MakerSubmitRequest
    {
        public string? MakerFeedback { get; set; }
        [Required]
        public MakerRecommendation Recommendation { get; set; }
    }

    [HttpPost("{claimId}/submit")]
    public async Task<ActionResult> SubmitReview([FromHeader(Name="X-User-Id")] Guid userId, Guid claimId, [FromBody] MakerSubmitRequest request)
    {
        var currentUser = await context.Users.FindAsync(userId);
        if (currentUser == null || currentUser.Role != UserModel.UserRole.Maker)
        {
            return Forbid();
        }
        var claim = await context.Claims.FindAsync(claimId);
        if (claim == null)
        {
            return NotFound();
        }
        if (claim.Status != ClaimStatus.Maker_In_Progress)
        {
            return BadRequest("Claim is not in a state that can be submitted for review.");
        }
        if (claim.ReviewerId != currentUser.UserId)
        {
            return Forbid();
        }

        claim.Status = ClaimStatus.Checker_Pending;
        claim.ReviewerId = null;

        var review = new Review
        {
            ClaimId = claimId,
            MakerId = currentUser.UserId,
            MakerFeedback = request.MakerFeedback,
            MakerRecommendation = request.Recommendation
        };

        context.Reviews.Add(review);
        await context.SaveChangesAsync();

        return Ok(new
        {
            Message = "Review submitted successfully and claim is now pending checker review.",
            MakerId = userId,
            ClaimId = claim.ClaimId,
            ReviewId = review.ReviewId,
            Status = claim.Status.ToString(),
            MakerSubmittedAt = DateTime.UtcNow
        });
    }

}