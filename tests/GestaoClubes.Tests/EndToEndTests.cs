using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using GestaoClubes.Core;
using Xunit;
namespace GestaoClubes.Tests;

public sealed class MySqlFactAttribute : FactAttribute { public MySqlFactAttribute() { if (Environment.GetEnvironmentVariable("CLUBS_TEST_URL") == null) Skip = "Execute scripts/Test.ps1 -Integration com API/MySQL disponíveis."; } }
public class EndToEndTests
{
    static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web) { Converters = { new JsonStringEnumConverter() } };
    static async Task<JsonElement> Read(HttpResponseMessage response) { response.EnsureSuccessStatusCode(); return JsonDocument.Parse(await response.Content.ReadAsStringAsync()).RootElement.Clone(); }
    static Task<HttpResponseMessage> Post(HttpClient c, string path, object body) => c.PostAsJsonAsync(path, body, Json);
    static Task<HttpResponseMessage> Put(HttpClient c, string path, object body) => c.PutAsJsonAsync(path, body, Json);
    static async Task<HttpClient> Login(string email, string password) { var c = new HttpClient { BaseAddress = new Uri(Environment.GetEnvironmentVariable("CLUBS_TEST_URL")!) }; var token = await Read(await Post(c, "/api/auth/login", new LoginRequest(email, password))); c.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token.GetProperty("token").GetString()); return c; }
    [MySqlFact]
    public async Task CompleteWorkflowAndTenantIsolation()
    {
        var password = Environment.GetEnvironmentVariable("Seed__Password")!; var suffix = Guid.NewGuid().ToString("N");
        using var root = await Login("superadmin@example.invalid", password);
        async Task<int> Club(string name) { var club = await Read(await Post(root, "/api/clubs", new ClubInput(name, "TEST", "Rua teste", "Centro", "Foz", "PR", "", "test@example.invalid", 75))); return club.GetProperty("id").GetInt32(); }
        var a = await Club("Teste A " + suffix); var b = await Club("Teste B " + suffix);
        using var anon = new HttpClient { BaseAddress = root.BaseAddress }; var email = $"member{suffix}@example.invalid";
        var registration = new RegisterRequest(a, "Pessoa Teste", email, password, MemberType.Associado, "52998224725", new DateOnly(1990, 1, 1), "Rua A", "Centro", "Foz", "PR", "", "", "", "Pesca", true);
        Assert.Equal(HttpStatusCode.OK, (await Post(anon, "/api/auth/register", registration)).StatusCode);
        using var member = await Login(email, password);
        Assert.Equal(HttpStatusCode.Forbidden, (await member.GetAsync($"/api/clubs/{a}/events")).StatusCode);
        var members = await Read(await root.GetAsync($"/api/clubs/{a}/members")); var id = members[0].GetProperty("id").GetInt32();
        Assert.Equal(HttpStatusCode.NoContent, (await Put(root, $"/api/clubs/{a}/members/{id}/approval", new ApprovalInput(Approval.Aprovado, Role.Associado, MemberType.Associado))).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await member.GetAsync($"/api/clubs/{a}/events")).StatusCode);
        foreach (var module in new[] { "events", "members", "finance", "reservations", "tickets", "audit", "profile", "dependents" }) Assert.Equal(HttpStatusCode.Forbidden, (await member.GetAsync($"/api/clubs/{b}/{module}")).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await Post(member, $"/api/clubs/{a}/charges", new ChargeInput(id, "2027-01", new DateOnly(2027, 1, 5)))).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await Put(root, $"/api/clubs/{b}/members/{id}/approval", new ApprovalInput(Approval.Aprovado, Role.Presidente, MemberType.Fundador))).StatusCode);
        Assert.Equal(HttpStatusCode.NoContent, (await Post(root, $"/api/clubs/{a}/charges", new ChargeInput(id, "2027-01", new DateOnly(2027, 1, 5)))).StatusCode);
        Assert.Equal(HttpStatusCode.Conflict, (await Post(root, $"/api/clubs/{a}/charges", new ChargeInput(id, "2027-01", new DateOnly(2027, 1, 5)))).StatusCode);
        var finance = await Read(await member.GetAsync($"/api/clubs/{a}/finance")); Assert.Equal(75, finance.GetProperty("outstanding").GetDecimal()); var chargeId = finance.GetProperty("charges")[0].GetProperty("id").GetInt32();
        var treasuryEmail=$"treasury{suffix}@example.invalid";
        Assert.Equal(HttpStatusCode.OK,(await Post(anon,"/api/auth/register",registration with{Email=treasuryEmail,Cpf="11144477735"})).StatusCode);
        var treasuryMembers=await Read(await root.GetAsync($"/api/clubs/{a}/members"));
        var treasuryId=treasuryMembers.EnumerateArray().Single(x=>x.GetProperty("email").GetString()==treasuryEmail).GetProperty("id").GetInt32();
        Assert.Equal(HttpStatusCode.NoContent,(await Put(root,$"/api/clubs/{a}/members/{treasuryId}/approval",new ApprovalInput(Approval.Aprovado,Role.Tesoureiro,MemberType.Associado))).StatusCode);
        using var treasury=await Login(treasuryEmail,password);
        Assert.Equal(HttpStatusCode.Forbidden,(await treasury.GetAsync($"/api/clubs/{a}/members")).StatusCode);
        var treasuryFinance=await Read(await treasury.GetAsync($"/api/clubs/{a}/finance"));
        Assert.Equal(2,treasuryFinance.GetProperty("eligibleMembers").GetArrayLength());
        Assert.Equal(HttpStatusCode.Forbidden,(await treasury.GetAsync($"/api/clubs/{b}/finance")).StatusCode);
        Assert.Equal(HttpStatusCode.NoContent, (await Post(treasury, $"/api/clubs/{a}/charges/{chargeId}/payment", new PaymentInput("Teste manual"))).StatusCode);
        Assert.Equal(HttpStatusCode.Conflict, (await Post(root, $"/api/clubs/{a}/charges/{chargeId}/payment", new PaymentInput("Duplicado"))).StatusCode);
        var start = DateTime.UtcNow.AddDays(7); var reservation = new ReservationInput("SALAO SOCIAL", "Feijoada teste", start, start.AddDays(1));
        var race = await Task.WhenAll(Post(member, $"/api/clubs/{a}/reservations", reservation), Post(member, $"/api/clubs/{a}/reservations", reservation)); Assert.Single(race, x => x.StatusCode == HttpStatusCode.OK); Assert.Single(race, x => x.StatusCode == HttpStatusCode.Conflict);
        var reservationId = (await Read(race.Single(x => x.StatusCode == HttpStatusCode.OK))).GetProperty("id").GetInt32();
        Assert.Equal(HttpStatusCode.NoContent, (await Put(root, $"/api/clubs/{a}/reservations/{reservationId}/status", new ReservationDecision(ReservationStatus.Aprovada))).StatusCode);
        var agenda = await Read(await anon.GetAsync($"/api/public/clubs/{a}/agenda")); Assert.Single(agenda.EnumerateArray()); Assert.DoesNotContain("cpf", agenda.GetRawText(), StringComparison.OrdinalIgnoreCase); Assert.DoesNotContain("purpose", agenda.GetRawText(), StringComparison.OrdinalIgnoreCase);
        Assert.Equal(HttpStatusCode.OK, (await Post(member, $"/api/clubs/{a}/reservations", reservation with { StartUtc = reservation.EndUtc, EndUtc = reservation.EndUtc.AddHours(2) })).StatusCode);
        Assert.Equal(HttpStatusCode.NoContent, (await Post(member, $"/api/clubs/{a}/dependents", new DependentInput("Dependente Teste", new DateOnly(2020, 1, 1), "Filho"))).StatusCode);
        var dependents = await Read(await member.GetAsync($"/api/clubs/{a}/dependents")); Assert.Equal("Dependente Teste", dependents[0].GetProperty("name").GetString());
        Assert.Equal(HttpStatusCode.NoContent, (await Post(member, $"/api/clubs/{a}/tickets", new TicketInput("Reclamacao", "Mensagem acadêmica de teste"))).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await Post(root, $"/api/clubs/{a}/events", new EventInput("Evento fechado", start, start.AddHours(1), false, false))).StatusCode);
        var events = await Read(await anon.GetAsync($"/api/public/clubs/{a}/events")); Assert.Empty(events.EnumerateArray());
        Assert.Equal(HttpStatusCode.OK,(await Post(anon,"/api/auth/register",registration with{ClubId=b})).StatusCode);
        var me=await Read(await member.GetAsync("/api/me"));Assert.Equal(2,me.GetProperty("memberships").GetArrayLength());
        Assert.Equal(HttpStatusCode.Forbidden,(await member.GetAsync($"/api/clubs/{b}/finance")).StatusCode);
        Assert.Equal(HttpStatusCode.NoContent,(await Put(root,$"/api/clubs/{a}/members/{id}/approval",new ApprovalInput(Approval.Aprovado,Role.Convidado,MemberType.Convidado))).StatusCode);
        var guestEvents=await Read(await member.GetAsync($"/api/clubs/{a}/events"));Assert.Empty(guestEvents.EnumerateArray());
        Assert.Equal(HttpStatusCode.NoContent, (await Put(root, $"/api/clubs/{a}/members/{id}/approval", new ApprovalInput(Approval.Suspenso, Role.Associado, MemberType.Associado))).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await member.GetAsync($"/api/clubs/{a}/finance")).StatusCode);
    }
}
