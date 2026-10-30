using GestaoClubes.Core;
using Microsoft.EntityFrameworkCore;
namespace GestaoClubes.Api;

public class Club
{
    public int Id { get; set; }
    public string Name { get; set; } = ""; public string Acronym { get; set; } = "";
    public string Address { get; set; } = ""; public string Neighborhood { get; set; } = ""; public string City { get; set; } = "";
    public string State { get; set; } = ""; public string Phone { get; set; } = ""; public string Email { get; set; } = ""; public decimal MonthlyFee { get; set; }
}
public class Account { public int Id { get; set; } public string Name { get; set; } = ""; public string Email { get; set; } = ""; public string PasswordHash { get; set; } = ""; public bool SuperAdmin { get; set; } }
public class Membership
{
    public int Id { get; set; }
    public int ClubId { get; set; }
    public Club Club { get; set; } = null!; public int AccountId { get; set; }
    public Account Account { get; set; } = null!;
    public Role Role { get; set; }
    public MemberType Type { get; set; }
    public Approval Status { get; set; }
    public string CpfEncrypted { get; set; } = ""; public string? CpfHash { get; set; }
    public string BirthEncrypted { get; set; } = "";
    public string Address { get; set; } = ""; public string Neighborhood { get; set; } = ""; public string City { get; set; } = ""; public string State { get; set; } = "";
    public string Phone { get; set; } = ""; public string Sex { get; set; } = ""; public string MaritalStatus { get; set; } = ""; public string Interests { get; set; } = "";
    public DateTime PrivacyAcceptedUtc { get; set; }
}
public class Dependent { public int Id { get; set; } public int MembershipId { get; set; } public Membership Membership { get; set; } = null!; public string NameEncrypted { get; set; } = ""; public string BirthEncrypted { get; set; } = ""; public string Relationship { get; set; } = ""; }
public class ClubEvent { public int Id { get; set; } public int ClubId { get; set; } public Club Club { get; set; } = null!; public string Title { get; set; } = ""; public DateTime StartUtc { get; set; } public DateTime EndUtc { get; set; } public bool Public { get; set; } public bool Cancelled { get; set; } }
public class Reservation { public int Id { get; set; } public int ClubId { get; set; } public Club Club { get; set; } = null!; public int MembershipId { get; set; } public Membership Membership { get; set; } = null!; public string Space { get; set; } = ""; public string Purpose { get; set; } = ""; public DateTime StartUtc { get; set; } public DateTime EndUtc { get; set; } public ReservationStatus Status { get; set; } }
public class Charge { public int Id { get; set; } public int MembershipId { get; set; } public Membership Membership { get; set; } = null!; public string Period { get; set; } = ""; public DateOnly DueDate { get; set; } public decimal Amount { get; set; } public DateTime? PaidUtc { get; set; } public string? Reference { get; set; } }
public class Ticket { public int Id { get; set; } public int MembershipId { get; set; } public Membership Membership { get; set; } = null!; public string Kind { get; set; } = ""; public string Text { get; set; } = ""; public TicketStatus Status { get; set; } public string? Response { get; set; } public DateTime CreatedUtc { get; set; } }
public class Audit { public long Id { get; set; } public int? ClubId { get; set; } public int ActorId { get; set; } public string Action { get; set; } = ""; public DateTime AtUtc { get; set; } }
public class ClubDb(DbContextOptions<ClubDb> options) : DbContext(options)
{
    public DbSet<Club> Clubs => Set<Club>(); public DbSet<Account> Accounts => Set<Account>(); public DbSet<Membership> Memberships => Set<Membership>();
    public DbSet<Dependent> Dependents => Set<Dependent>(); public DbSet<ClubEvent> Events => Set<ClubEvent>(); public DbSet<Reservation> Reservations => Set<Reservation>();
    public DbSet<Charge> Charges => Set<Charge>(); public DbSet<Ticket> Tickets => Set<Ticket>(); public DbSet<Audit> Audits => Set<Audit>();
    protected override void OnModelCreating(ModelBuilder b)
    {
        b.Entity<Account>().HasIndex(x => x.Email).IsUnique(); b.Entity<Account>().Property(x => x.Email).HasMaxLength(160);
        b.Entity<Membership>().HasIndex(x => new { x.ClubId, x.AccountId }).IsUnique(); b.Entity<Membership>().HasIndex(x => new { x.ClubId, x.CpfHash }).IsUnique(); b.Entity<Membership>().Property(x => x.CpfHash).HasMaxLength(64);
        b.Entity<Charge>().HasIndex(x => new { x.MembershipId, x.Period }).IsUnique(); b.Entity<Charge>().Property(x => x.Period).HasMaxLength(7);
        b.Entity<Club>().Property(x => x.MonthlyFee).HasPrecision(12, 2); b.Entity<Charge>().Property(x => x.Amount).HasPrecision(12, 2);
        b.Entity<Reservation>().Property(x => x.Space).HasMaxLength(80); b.Entity<Reservation>().HasIndex(x => new { x.ClubId, x.Space, x.StartUtc });
        foreach (var fk in b.Model.GetEntityTypes().SelectMany(x => x.GetForeignKeys())) fk.DeleteBehavior = DeleteBehavior.Restrict;
    }
}
