using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using System.Text.Json.Serialization;
using System.Threading.RateLimiting;
using GestaoClubes.Api;
using GestaoClubes.Core;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;

var builder = WebApplication.CreateBuilder(args);
var config = builder.Configuration; builder.WebHost.ConfigureKestrel(o => o.Limits.MaxRequestBodySize = 65536);
var connection = new MySqlConnector.MySqlConnectionStringBuilder(config.GetConnectionString("Clubs") ?? throw new InvalidOperationException("Configure ConnectionStrings__Clubs."));
if (connection.Database != "gestaoclubes" || connection.UserID != "gestaoclubes_app") throw new InvalidOperationException("Use exclusivamente Database=gestaoclubes e User=gestaoclubes_app. Conexão recusada para proteger outros projetos.");
foreach (var key in new[] { "Jwt:Key", "Security:CpfKey" }) if ((config[key]?.Length ?? 0) < 32) throw new InvalidOperationException($"Configure {key} com pelo menos 32 caracteres.");
builder.Services.AddDbContext<ClubDb>(o => o.UseMySql(config.GetConnectionString("Clubs"), new MySqlServerVersion(new Version(8, 0, 36))));
var protection = builder.Services.AddDataProtection().SetApplicationName("GestaoClubes").PersistKeysToFileSystem(new DirectoryInfo(config["Security:KeyPath"] ?? "secrets/keys")); if (OperatingSystem.IsWindows()) protection.ProtectKeysWithDpapi();
builder.Services.AddHttpContextAccessor(); builder.Services.AddScoped<Access>(); builder.Services.AddScoped<PersonalData>(); builder.Services.AddScoped<PasswordHasher<Account>>();
builder.Services.ConfigureHttpJsonOptions(o => o.SerializerOptions.Converters.Add(new JsonStringEnumConverter()));
builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme).AddJwtBearer(o => o.TokenValidationParameters = new TokenValidationParameters
{
    ValidateIssuer = true,
    ValidIssuer = "GestaoClubes",
    ValidateAudience = true,
    ValidAudience = "GestaoClubes",
    ValidateLifetime = true,
    ClockSkew = TimeSpan.FromSeconds(15),
    ValidateIssuerSigningKey = true,
    IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(config["Jwt:Key"]!))
});
builder.Services.AddAuthorization();
builder.Services.AddRateLimiter(o => { o.RejectionStatusCode = 429; o.AddPolicy("auth", c => RateLimitPartition.GetFixedWindowLimiter(c.Connection.RemoteIpAddress?.ToString() ?? "local", _ => new FixedWindowRateLimiterOptions { PermitLimit = 15, Window = TimeSpan.FromMinutes(1) })); });
var app = builder.Build();
app.Use(async (ctx, next) => { try { await next(); } catch (DomainError e) { ctx.Response.StatusCode = e.Status; await ctx.Response.WriteAsJsonAsync(new { error = e.Message }); } catch (DbUpdateException) { ctx.Response.StatusCode = 409; await ctx.Response.WriteAsJsonAsync(new { error = "Conflito de dados. Verifique duplicidade ou vínculos." }); } });
app.UseRateLimiter(); app.UseAuthentication(); app.UseAuthorization();
if (args.Contains("--migrate") || args.Contains("--seed"))
{
    using var scope = app.Services.CreateScope(); var db = scope.ServiceProvider.GetRequiredService<ClubDb>(); await db.Database.MigrateAsync();
    if (args.Contains("--seed")) await Seed.Run(db, scope.ServiceProvider.GetRequiredService<PasswordHasher<Account>>(), config);
    return;
}
app.MapGet("/health", () => Results.Ok(new { service = "GestaoClubes.Api", status = "up" }));
var auth = app.MapGroup("/api/auth").AddEndpointFilter<ValidateInput>().RequireRateLimiting("auth");
auth.MapPost("/login", async (LoginRequest input, ClubDb db, PasswordHasher<Account> hasher) =>
{
    var account = await db.Accounts.SingleOrDefaultAsync(x => x.Email == input.Email.Trim().ToLowerInvariant());
    if (account is null || hasher.VerifyHashedPassword(account, account.PasswordHash, input.Password) == PasswordVerificationResult.Failed) return Results.Unauthorized();
    var expires = DateTime.UtcNow.AddMinutes(30);
    var jwt = new JwtSecurityToken("GestaoClubes", "GestaoClubes", new[] { new Claim(ClaimTypes.NameIdentifier, account.Id.ToString()), new Claim(ClaimTypes.Name, account.Name) }, expires: expires, signingCredentials: new SigningCredentials(new SymmetricSecurityKey(Encoding.UTF8.GetBytes(config["Jwt:Key"]!)), SecurityAlgorithms.HmacSha256));
    return Results.Ok(new TokenResponse(new JwtSecurityTokenHandler().WriteToken(jwt), expires));
});
auth.MapPost("/register", async (RegisterRequest input, ClubDb db, PersonalData privacy, PasswordHasher<Account> hasher) =>
{
    if (!input.PrivacyAccepted || !Rules.ValidCpf(input.Cpf) || !Rules.ValidBirth(input.BirthDate)) throw new DomainError(400, "Aceite a informação de privacidade e informe CPF e nascimento válidos.");
    if (!await db.Clubs.AnyAsync(x => x.Id == input.ClubId)) throw new DomainError(404, "Clube inexistente.");
    var email = input.Email.Trim().ToLowerInvariant();
    var account = await db.Accounts.SingleOrDefaultAsync(x => x.Email == email);
    if(account != null) {
        if(hasher.VerifyHashedPassword(account, account.PasswordHash, input.Password) == PasswordVerificationResult.Failed || await db.Memberships.AnyAsync(x => x.AccountId == account.Id && x.ClubId == input.ClubId))
            throw new DomainError(409, "Cadastro indisponível. Verifique as credenciais e o vínculo existente.");
    } else {
        account = new Account { Name = input.Name.Trim(), Email = email };
        account.PasswordHash = hasher.HashPassword(account, input.Password);
    }
    db.Memberships.Add(new Membership { Account = account, ClubId = input.ClubId, Role = input.Type == MemberType.Convidado ? Role.Convidado : input.Type == MemberType.Simpatizante ? Role.Simpatizante : Role.Associado, Type = input.Type == MemberType.Fundador ? MemberType.Associado : input.Type, Status = Approval.Pendente, CpfEncrypted = privacy.Encrypt(input.Cpf), CpfHash = privacy.CpfHash(input.Cpf), BirthEncrypted = privacy.Encrypt(input.BirthDate.ToString("yyyy-MM-dd")), Address = input.Address, Neighborhood = input.Neighborhood, City = input.City, State = input.State, Phone = input.Phone ?? "", Sex = input.Sex ?? "", MaritalStatus = input.MaritalStatus ?? "", Interests = input.Interests ?? "", PrivacyAcceptedUtc = DateTime.UtcNow });
    db.Audits.Add(new Audit { ClubId = input.ClubId, Action = "Cadastro público pendente", AtUtc = DateTime.UtcNow }); await db.SaveChangesAsync(); return Results.Ok(new { message = "Cadastro recebido. Aguarde aprovação do clube." });
});
app.MapGet("/api/public/clubs", async (ClubDb db) => await db.Clubs.Select(x => new { x.Id, x.Name, x.Acronym, x.City, x.State, x.Email, x.Phone }).ToListAsync());
app.MapGet("/api/public/clubs/{clubId:int}/events", async (int clubId, ClubDb db) => await db.Events.Where(x => x.ClubId == clubId && x.Public && !x.Cancelled).Select(x => new { x.Id, x.Title, x.StartUtc, x.EndUtc }).ToListAsync());
app.MapGet("/api/public/clubs/{clubId:int}/agenda", async (int clubId, ClubDb db) => await db.Reservations.Where(x => x.ClubId == clubId && x.Status == ReservationStatus.Aprovada).Select(x => new { x.Space, x.StartUtc, x.EndUtc }).ToListAsync());
var api = app.MapGroup("/api").RequireAuthorization().AddEndpointFilter<ValidateInput>();
api.MapGet("/me", async (Access access, ClubDb db) => new { superAdmin = await access.IsSuper(), memberships = await db.Memberships.Where(x => x.AccountId == access.Actor).Select(x => new { x.Id, x.ClubId, club = x.Club.Name, x.Role, x.Type, x.Status, canVote = x.Status == Approval.Aprovado && (x.Type == MemberType.Associado || x.Type == MemberType.Fundador) }).ToListAsync() });
api.MapPost("/clubs", async (ClubInput input, ClubDb db, Access access) =>
{
    if (!await access.IsSuper()) throw new DomainError(403, "Somente superadmin."); var club = new Club(); ApplyClub(club, input); db.Clubs.Add(club); await access.Save(null, "Criou clube"); return Results.Ok(club);
});
api.MapGet("/clubs/{clubId:int}", async (int clubId, ClubDb db, Access access) => { await access.Check(clubId); return await db.Clubs.SingleAsync(x => x.Id == clubId); });
api.MapPut("/clubs/{clubId:int}", async (int clubId, ClubInput input, ClubDb db, Access access) => { await access.Check(clubId, Role.Presidente); var club = await db.Clubs.SingleAsync(x => x.Id == clubId); ApplyClub(club, input); await access.Save(clubId, "Atualizou clube"); return Results.Ok(club); });
api.MapGet("/clubs/{clubId:int}/members", async (int clubId, ClubDb db, Access access) => { await access.Check(clubId, Role.Presidente); return await db.Memberships.Where(x => x.ClubId == clubId).Select(x => new { x.Id, x.Account.Name, x.Account.Email, x.Role, x.Type, x.Status, x.City, x.Interests }).ToListAsync(); });
api.MapPut("/clubs/{clubId:int}/members/{id:int}/approval", async (int clubId, int id, ApprovalInput input, ClubDb db, Access access) =>
{
    await access.Check(clubId, Role.Presidente); var member = await db.Memberships.SingleOrDefaultAsync(x => x.ClubId == clubId && x.Id == id) ?? throw new DomainError(404, "Vínculo inexistente.");
    if (member.AccountId == access.Actor) throw new DomainError(400, "Não altere o próprio acesso.");
    if (input.Role == Role.Presidente && !await access.IsSuper()) throw new DomainError(403, "Somente superadmin nomeia presidente.");
    if (member.Role == Role.Presidente && !await access.IsSuper()) throw new DomainError(403, "Somente superadmin altera presidente.");
    member.Status = input.Status; member.Role = input.Role; member.Type = input.Type; await access.Save(clubId, $"Aprovação/perfil vínculo {id}: {input.Status}/{input.Role}/{input.Type}"); return Results.NoContent();
});
api.MapGet("/clubs/{clubId:int}/profile", async (int clubId, ClubDb db, Access access, PersonalData privacy) => { var m = await access.Self(clubId); await access.Save(clubId, "Consultou próprios dados pessoais"); return Results.Ok(new { m.Id, m.AccountId, m.Address, m.Neighborhood, m.City, m.State, m.Phone, m.Sex, m.MaritalStatus, m.Interests, cpf = (m.CpfEncrypted.Length == 0 ? null : privacy.Decrypt(m.CpfEncrypted)), birthDate = (m.BirthEncrypted.Length == 0 ? null : privacy.Decrypt(m.BirthEncrypted)) }); });
api.MapGet("/clubs/{clubId:int}/dependents", async (int clubId, ClubDb db, Access access, PersonalData privacy) => { var m = await access.Self(clubId); var rows = await db.Dependents.Where(x => x.MembershipId == m.Id).ToListAsync(); await access.Save(clubId, "Consultou dependentes próprios"); return rows.Select(x => new { x.Id, name = privacy.Decrypt(x.NameEncrypted), birthDate = privacy.Decrypt(x.BirthEncrypted), x.Relationship }); });
api.MapPost("/clubs/{clubId:int}/dependents", async (int clubId, DependentInput input, ClubDb db, Access access, PersonalData privacy) => { var m = await access.Self(clubId); if (!Rules.ValidBirth(input.BirthDate)) throw new DomainError(400, "Nascimento inválido."); db.Dependents.Add(new Dependent { MembershipId = m.Id, NameEncrypted = privacy.Encrypt(input.Name), BirthEncrypted = privacy.Encrypt(input.BirthDate.ToString("yyyy-MM-dd")), Relationship = input.Relationship }); await access.Save(clubId, "Cadastrou dependente"); return Results.NoContent(); });
api.MapDelete("/clubs/{clubId:int}/dependents/{id:int}", async (int clubId, int id, ClubDb db, Access access) => { var m = await access.Self(clubId); var row = await db.Dependents.SingleOrDefaultAsync(x => x.Id == id && x.MembershipId == m.Id) ?? throw new DomainError(404, "Dependente inexistente."); db.Remove(row); await access.Save(clubId, $"Removeu dependente {id}"); return Results.NoContent(); });
api.MapGet("/clubs/{clubId:int}/events", async (int clubId, ClubDb db, Access access) => { var m = await access.Check(clubId); var privateAccess = m == null || m.Type is MemberType.Associado or MemberType.Fundador || m.Role is Role.Presidente or Role.Tesoureiro; return await db.Events.Where(x => x.ClubId == clubId && (privateAccess || x.Public)).ToListAsync(); });
api.MapPost("/clubs/{clubId:int}/events", async (int clubId, EventInput input, ClubDb db, Access access) => { await access.Check(clubId, Role.Presidente); if (!Rules.ValidInterval(input.StartUtc, input.EndUtc)) throw new DomainError(400, "Intervalo UTC inválido (máximo 31 dias)."); var row = new ClubEvent { ClubId = clubId, Title = input.Title, StartUtc = input.StartUtc, EndUtc = input.EndUtc, Public = input.Public, Cancelled = input.Cancelled }; db.Events.Add(row); await access.Save(clubId, "Criou evento"); return Results.Ok(row); });
api.MapPut("/clubs/{clubId:int}/events/{id:int}", async (int clubId, int id, EventInput input, ClubDb db, Access access) => { await access.Check(clubId, Role.Presidente); if (!Rules.ValidInterval(input.StartUtc, input.EndUtc)) throw new DomainError(400, "Intervalo UTC inválido."); var row = await db.Events.SingleOrDefaultAsync(x => x.Id == id && x.ClubId == clubId) ?? throw new DomainError(404, "Evento inexistente."); row.Title = input.Title; row.StartUtc = input.StartUtc; row.EndUtc = input.EndUtc; row.Public = input.Public; row.Cancelled = input.Cancelled; await access.Save(clubId, $"Atualizou evento {id}"); return Results.Ok(row); });
api.MapGet("/clubs/{clubId:int}/reservations", async (int clubId, ClubDb db, Access access) => { var m = await access.Check(clubId); return await db.Reservations.Where(x => x.ClubId == clubId && (m == null || m.Role == Role.Presidente || x.MembershipId == m.Id)).Select(x => new { x.Id, x.MembershipId, x.Space, x.Purpose, x.StartUtc, x.EndUtc, x.Status }).ToListAsync(); });
api.MapPost("/clubs/{clubId:int}/reservations", async (int clubId, ReservationInput input, ClubDb db, Access access) =>
{
    var member = await access.Self(clubId); if (!Rules.ValidInterval(input.StartUtc, input.EndUtc) || input.StartUtc < DateTime.UtcNow) throw new DomainError(400, "Informe intervalo futuro em UTC, até 31 dias.");
    var space = input.Space.Trim().ToUpperInvariant(); if (space is not ("SALAO SOCIAL" or "CHURRASQUEIRA")) throw new DomainError(400, "Escolha SALAO SOCIAL ou CHURRASQUEIRA.");
    await using var tx = await db.Database.BeginTransactionAsync();
    await db.Clubs.FromSqlInterpolated($"SELECT * FROM Clubs WHERE Id={clubId} FOR UPDATE").ToListAsync();
    if (await db.Reservations.AnyAsync(x => x.ClubId == clubId && x.Space == space && (x.Status == ReservationStatus.Pendente || x.Status == ReservationStatus.Aprovada) && x.StartUtc < input.EndUtc && input.StartUtc < x.EndUtc)) throw new DomainError(409, "Espaço ocupado neste intervalo.");
    var row = new Reservation { ClubId = clubId, MembershipId = member.Id, Space = space, Purpose = input.Purpose, StartUtc = input.StartUtc, EndUtc = input.EndUtc, Status = ReservationStatus.Pendente }; db.Reservations.Add(row); await access.Save(clubId, "Solicitou reserva"); await tx.CommitAsync(); return Results.Ok(new { row.Id, row.Status });
});
api.MapPut("/clubs/{clubId:int}/reservations/{id:int}/status", async (int clubId, int id, ReservationDecision input, ClubDb db, Access access) =>
{
    var member = await access.Check(clubId); await using var tx = await db.Database.BeginTransactionAsync(); await db.Clubs.FromSqlInterpolated($"SELECT * FROM Clubs WHERE Id={clubId} FOR UPDATE").ToListAsync(); var row = await db.Reservations.SingleOrDefaultAsync(x => x.Id == id && x.ClubId == clubId) ?? throw new DomainError(404, "Reserva inexistente."); var admin = member == null || member.Role == Role.Presidente;
    if (!admin && (row.MembershipId != member!.Id || input.Status != ReservationStatus.Cancelada)) throw new DomainError(403, "Sem permissão.");
    if (row.Status is ReservationStatus.Cancelada or ReservationStatus.Rejeitada || input.Status == ReservationStatus.Pendente || (row.Status == ReservationStatus.Aprovada && input.Status != ReservationStatus.Cancelada)) throw new DomainError(400, "Transição inválida.");
    row.Status = input.Status; await access.Save(clubId, $"Reserva {id}: {input.Status}"); await tx.CommitAsync(); return Results.NoContent();
});
api.MapGet("/clubs/{clubId:int}/finance", async (int clubId, ClubDb db, Access access) => { var m = await access.Check(clubId); var all = m == null || m.Role is Role.Presidente or Role.Tesoureiro; var rows = await db.Charges.Where(x => x.Membership.ClubId == clubId && (all || x.MembershipId == m!.Id)).Select(x => new { x.Id, x.MembershipId, name = x.Membership.Account.Name, x.Period, x.DueDate, x.Amount, x.PaidUtc, x.Reference }).ToListAsync(); var eligible = all ? await db.Memberships.Where(x => x.ClubId == clubId && x.Status == Approval.Aprovado).Select(x => new { x.Id, x.Account.Name }).ToListAsync() : null; return Results.Ok(new { eligibleMembers = eligible, charges = rows, total = rows.Sum(x => x.Amount), received = rows.Where(x => x.PaidUtc != null).Sum(x => x.Amount), outstanding = rows.Where(x => x.PaidUtc == null).Sum(x => x.Amount), overdue = rows.Where(x => x.PaidUtc == null && x.DueDate < DateOnly.FromDateTime(DateTime.UtcNow)).Sum(x => x.Amount) }); });
api.MapPost("/clubs/{clubId:int}/charges", async (int clubId, ChargeInput input, ClubDb db, Access access) => { await access.Check(clubId, Role.Presidente, Role.Tesoureiro); if (!await db.Memberships.AnyAsync(x => x.Id == input.MembershipId && x.ClubId == clubId && x.Status == Approval.Aprovado)) throw new DomainError(400, "Associado aprovado não encontrado neste clube."); var fee = await db.Clubs.Where(x => x.Id == clubId).Select(x => x.MonthlyFee).SingleAsync(); if (fee <= 0) throw new DomainError(400, "Configure valor mensal positivo."); db.Charges.Add(new Charge { MembershipId = input.MembershipId, Period = input.Period, DueDate = input.DueDate, Amount = fee }); await access.Save(clubId, $"Gerou mensalidade vínculo {input.MembershipId} / {input.Period}"); return Results.NoContent(); });
api.MapPost("/clubs/{clubId:int}/charges/{id:int}/payment", async (int clubId, int id, PaymentInput input, ClubDb db, Access access) => { await access.Check(clubId, Role.Presidente, Role.Tesoureiro); await using var tx = await db.Database.BeginTransactionAsync(); await db.Clubs.FromSqlInterpolated($"SELECT * FROM Clubs WHERE Id={clubId} FOR UPDATE").ToListAsync(); var row = await db.Charges.SingleOrDefaultAsync(x => x.Id == id && x.Membership.ClubId == clubId) ?? throw new DomainError(404, "Mensalidade inexistente."); if (row.PaidUtc != null) throw new DomainError(409, "Pagamento já registrado."); row.PaidUtc = DateTime.UtcNow; row.Reference = input.Reference; await access.Save(clubId, $"Baixa manual mensalidade {id}"); await tx.CommitAsync(); return Results.NoContent(); });
api.MapGet("/clubs/{clubId:int}/tickets", async (int clubId, ClubDb db, Access access) => { var m = await access.Check(clubId); return await db.Tickets.Where(x => x.Membership.ClubId == clubId && (m == null || m.Role == Role.Presidente || x.MembershipId == m.Id)).Select(x => new { x.Id, x.Kind, x.Text, x.Status, x.Response, x.CreatedUtc }).ToListAsync(); });
api.MapPost("/clubs/{clubId:int}/tickets", async (int clubId, TicketInput input, ClubDb db, Access access) => { var m = await access.Self(clubId); db.Tickets.Add(new Ticket { MembershipId = m.Id, Kind = input.Kind, Text = input.Text, CreatedUtc = DateTime.UtcNow }); await access.Save(clubId, "Abriu atendimento"); return Results.NoContent(); });
api.MapPut("/clubs/{clubId:int}/tickets/{id:int}", async (int clubId, int id, TicketReply input, ClubDb db, Access access) => { await access.Check(clubId, Role.Presidente); var row = await db.Tickets.SingleOrDefaultAsync(x => x.Id == id && x.Membership.ClubId == clubId) ?? throw new DomainError(404, "Atendimento inexistente."); row.Status = input.Status; row.Response = input.Response; await access.Save(clubId, $"Respondeu atendimento {id}"); return Results.NoContent(); });
api.MapGet("/clubs/{clubId:int}/audit", async (int clubId, ClubDb db, Access access) => { await access.Check(clubId, Role.Presidente); return await db.Audits.Where(x => x.ClubId == clubId).OrderByDescending(x => x.Id).Take(200).ToListAsync(); });
app.Run();
static void ApplyClub(Club c, ClubInput i) { c.Name = i.Name; c.Acronym = i.Acronym; c.Address = i.Address; c.Neighborhood = i.Neighborhood; c.City = i.City; c.State = i.State; c.Phone = i.Phone ?? ""; c.Email = i.Email; c.MonthlyFee = i.MonthlyFee; }
public partial class Program;
