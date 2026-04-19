using InsureZenAPI.Models;
using Microsoft.EntityFrameworkCore;

namespace InsureZenAPI.Data;

public class InsureZenContext(DbContextOptions<InsureZenContext> options) : DbContext(options)
{
    public DbSet<Claim> Claims { get; set; }
    public DbSet<Review> Reviews { get; set; }
    public DbSet<User> Users { get; set; }
    public DbSet<InsuranceCompany> InsuranceCompanies { get; set; }
}