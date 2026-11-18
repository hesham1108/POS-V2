using Microsoft.EntityFrameworkCore;

namespace StoresPOS.DAL.Context;

/// <summary>
/// Empty DbContext scaffold. Entities will be added in later phases.
/// </summary>
public class ApplicationDbContext : DbContext
{
    public ApplicationDbContext(DbContextOptions<ApplicationDbContext> options)
        : base(options)
    {
    }
}
