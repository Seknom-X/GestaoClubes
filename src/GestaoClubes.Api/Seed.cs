using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using GestaoClubes.Core;
namespace GestaoClubes.Api;

public static class Seed
{
    public static async Task Run(ClubDb db, PasswordHasher<Account> hasher, IConfiguration config)
    {
        var password = config["Seed:Password"] ?? throw new InvalidOperationException("Configure Seed__Password."); if (password.Length < 12) throw new InvalidOperationException("Senha seed exige 12 caracteres.");
        if (await db.Accounts.AnyAsync()) throw new InvalidOperationException("Seed recusado: banco já contém contas.");
        var clubs = new[] { new Club { Name = "Clube Foz", Acronym = "FOZ", City = "Foz do Iguaçu", State = "PR", Address = "Endereço demonstrativo", Neighborhood = "Centro", Email = "foz@example.invalid", MonthlyFee = 80 }, new Club { Name = "Clube Guaíra", Acronym = "GUA", City = "Guaíra", State = "PR", Address = "Endereço demonstrativo", Neighborhood = "Centro", Email = "guaira@example.invalid", MonthlyFee = 65 } };
        db.Clubs.AddRange(clubs); var root = new Account { Name = "Superadministrador", Email = "superadmin@example.invalid", SuperAdmin = true }; root.PasswordHash = hasher.HashPassword(root, password); db.Accounts.Add(root);
        foreach (var (club, index) in clubs.Select((c, i) => (c, i + 1))) foreach (var role in new[] { Role.Presidente, Role.Tesoureiro }) { var a = new Account { Name = $"{role} {club.Acronym}", Email = $"{role.ToString().ToLowerInvariant()}{index}@example.invalid" }; a.PasswordHash = hasher.HashPassword(a, password); db.Memberships.Add(new Membership { Club = club, Account = a, Role = role, Type = MemberType.Associado, Status = Approval.Aprovado, PrivacyAcceptedUtc = DateTime.UtcNow }); }
        db.Audits.Add(new Audit { Action = "Seed acadêmico explícito (contas administrativas sem perfil pessoal)", AtUtc = DateTime.UtcNow }); await db.SaveChangesAsync();
    }
}
