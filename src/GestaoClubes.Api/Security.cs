using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;
using System.ComponentModel.DataAnnotations;
using GestaoClubes.Core;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.EntityFrameworkCore;
namespace GestaoClubes.Api;

public sealed class DomainError(int status, string message) : Exception(message) { public int Status => status; }
public class Access(ClubDb db, IHttpContextAccessor context)
{
    public int Actor => int.Parse(context.HttpContext!.User.FindFirstValue(ClaimTypes.NameIdentifier)!);
    public async Task<bool> IsSuper() => await db.Accounts.AnyAsync(x => x.Id == Actor && x.SuperAdmin);
    public async Task<Membership?> Check(int clubId, params Role[] roles)
    {
        if (!await db.Clubs.AnyAsync(x => x.Id == clubId)) throw new DomainError(404, "Clube inexistente.");
        if (await IsSuper()) return null;
        var member = await db.Memberships.SingleOrDefaultAsync(x => x.ClubId == clubId && x.AccountId == Actor);
        if (member is null || member.Status != Approval.Aprovado || (roles.Length > 0 && !roles.Contains(member.Role))) throw new DomainError(403, "Sem permissão neste clube.");
        return member;
    }
    public async Task<Membership> Self(int clubId)
    {
        await Check(clubId);
        return await db.Memberships.SingleOrDefaultAsync(x => x.ClubId == clubId && x.AccountId == Actor && x.Status == Approval.Aprovado) ?? throw new DomainError(403, "É necessário vínculo aprovado.");
    }
    public async Task Save(int? clubId, string action) { db.Audits.Add(new Audit { ClubId = clubId, ActorId = Actor, Action = action, AtUtc = DateTime.UtcNow }); await db.SaveChangesAsync(); }
}
public class PersonalData(IDataProtectionProvider provider, IConfiguration config)
{
    readonly IDataProtector protector = provider.CreateProtector("GestaoClubes.Personal.v1");
    public string Encrypt(string value) => protector.Protect(value);
    public string Decrypt(string value) => protector.Unprotect(value);
    public string CpfHash(string value) => Convert.ToHexString(HMACSHA256.HashData(Encoding.UTF8.GetBytes(config["Security:CpfKey"]!), Encoding.UTF8.GetBytes(new string(value.Where(char.IsAsciiDigit).ToArray()))));
}
public class ValidateInput : IEndpointFilter
{
    public async ValueTask<object?> InvokeAsync(EndpointFilterInvocationContext context, EndpointFilterDelegate next)
    {
        foreach (var arg in context.Arguments.Where(x => x is not null && x.GetType().Namespace == "GestaoClubes.Core"))
        {
            var errors = new List<ValidationResult>(); if (!Validator.TryValidateObject(arg!, new ValidationContext(arg!), errors, true)) throw new DomainError(400, string.Join("; ", errors.Select(x => x.ErrorMessage)));
            foreach (var prop in arg!.GetType().GetProperties().Where(x => x.PropertyType.IsEnum)) if (!Enum.IsDefined(prop.PropertyType, prop.GetValue(arg)!)) throw new DomainError(400, "Opção inválida.");
        }
        return await next(context);
    }
}
