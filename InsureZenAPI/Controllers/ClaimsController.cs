using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using InsureZenAPI.Data;
using InsureZenAPI.Models;
using UserModel = InsureZenAPI.Models.User;
using System.ComponentModel.DataAnnotations;

namespace InsureZenAPI.Controllers;

public class QueryParameters
{
    public int Page { get; set; } = 1;
    public int Limit { get; set; } = 10;
    public string? Status { get; set; }
    public string? CompanyName { get; set; }
    public DateTime? FromDate { get; set; }
    public DateTime? ToDate { get; set; }
}

[ApiController]
[Route("api/[controller]")]
public class ClaimsController : ControllerBase
{
    private readonly InsureZenContext context;

    public ClaimsController(InsureZenContext context)
    {
        this.context = context;
    }

    [HttpGet]
    public async Task<ActionResult> GetClaims()
    {
        return Ok(await context.Claims.ToListAsync());
    }

    [HttpGet("{id}")]
    public async Task<ActionResult> GetClaim(Guid id)
    {
        var claim = await context.Claims.Include(c => c.Company).FirstOrDefaultAsync(c => c.ClaimId == id);
        if (claim == null)
        {
            return NotFound();
        }
        return Ok(claim);
    }

    public class ClaimSubmitRequest
    {
        [Required]
        public Guid CompanyId { get; set; }
        [Required, MinLength(1)]
        public string RawClaimData { get; set; } = string.Empty;
        public string? NormalizedClaimData { get; set; }
    }

    [HttpPost]
    public async Task<ActionResult> SubmitClaim([FromBody] ClaimSubmitRequest request)
    {
        var company = await context.InsuranceCompanies.FindAsync(request.CompanyId);
        if (company == null)
        {
            return BadRequest("Invalid company ID.");
        }
        var claim = new Claim
        {
            CompanyId = request.CompanyId,
            RawClaimData = request.RawClaimData,
            // For the purpose of this task we are sending the normalized claim data as part of the request, but in a real-world scenario we would implement company-specific normalization logic here
            NormalizedClaimData = request.NormalizedClaimData ?? NormalizeClaimData(request.RawClaimData, company)
        };

        context.Claims.Add(claim);
        await context.SaveChangesAsync();

        return StatusCode(201, new 
        { 
            Message = "Claim submitted successfully.",
            ClaimId = claim.ClaimId,
            Status = claim.Status.ToString(),
            SubmittedAt = DateTime.UtcNow
        });
    }

    [HttpPut("{id}")]
    public async Task<ActionResult> UpdateClaim(Guid id, [FromBody] Claim claim)
    {
        var existingClaim = await context.Claims.FindAsync(id);
        if (existingClaim == null)
        {
            return NotFound();
        }

        existingClaim.Status = claim.Status;
        existingClaim.NormalizedClaimData = claim.NormalizedClaimData;

        if (claim.Status == ClaimStatus.Completed)
        {
            existingClaim.CompletedAt = DateTime.UtcNow;
        }

        await context.SaveChangesAsync();
        return NoContent();
    }

    [HttpGet("history")]
    public async Task<ActionResult> ClaimsHistory([FromQuery] QueryParameters parameters)
    {
        var query = context.Claims.Include(c => c.Company).AsQueryable();

        if (!string.IsNullOrEmpty(parameters.Status) && Enum.TryParse<ClaimStatus>(parameters.Status, true, out var status))
        {
            query = query.Where(c => c.Status == status);
        }
        if (!string.IsNullOrEmpty(parameters.CompanyName))
        {
            query = query.Where(c => c.Company != null && c.Company.CompanyName.Contains(parameters.CompanyName));
        }
        if (parameters.FromDate.HasValue)
        {
            query = query.Where(c => c.SubmittedAt >= parameters.FromDate.Value);
        }
        if (parameters.ToDate.HasValue)
        {
            query = query.Where(c => c.SubmittedAt <= parameters.ToDate.Value);
        }

        var totalCount = await query.CountAsync();
        var totalPages = (int)Math.Ceiling(totalCount / (double)parameters.Limit);

        var claims = await query
            .Skip((parameters.Page - 1) * parameters.Limit)
            .Take(parameters.Limit)
            .Select(c => new
            {
                claimId = c.ClaimId,
                companyName = c.Company!.CompanyName,
                status = c.Status.ToString(),
                submittedAt = c.SubmittedAt,
                completedAt = c.CompletedAt
            })
            .ToListAsync();

        return Ok(new
        {
            items = claims,
            pagination = new
            {
                totalCount = totalCount,
                currentPage = parameters.Page,
                totalPages = totalPages,
                limit = parameters.Limit,
                hasNextPage = parameters.Page < totalPages,
                hasPrevPage = parameters.Page > 1
            }
        });
    }

    private static string NormalizeClaimData(string rawData, InsuranceCompany company)
    {
        // Placeholder for company-specific normalization logic
        // This should parse the raw data and transform it into the company's expected format
        return $"Normalized data for {company.CompanyName}: {rawData}";
    }
}