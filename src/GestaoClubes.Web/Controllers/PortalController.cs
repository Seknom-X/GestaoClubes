using System.Net.Http.Headers;
using System.Text.Json;
using System.Text.Json.Serialization;
using GestaoClubes.Core;
using GestaoClubes.Web.Models;
using Microsoft.AspNetCore.Mvc;
namespace GestaoClubes.Web.Controllers;

public class PortalController(IHttpClientFactory factory) : Controller
{
    static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web) { Converters = { new JsonStringEnumConverter() } };
    HttpClient Client() { var c = factory.CreateClient("api"); var token = HttpContext.Session.GetString("token"); if (token != null) c.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token); return c; }
    async Task<JsonElement> Get(string path) { using var c = Client(); var response = await c.GetAsync(path); if (!response.IsSuccessStatusCode) throw new HttpRequestException($"API: {(int)response.StatusCode}"); return JsonSerializer.Deserialize<JsonElement>(await response.Content.ReadAsStringAsync()); }
    public async Task<IActionResult> Index(int clubId = 0) { var clubs=await Get("/api/public/clubs"); if(!clubs.EnumerateArray().Any(x=>x.GetProperty("id").GetInt32()==clubId))clubId=clubs.GetArrayLength()>0?clubs[0].GetProperty("id").GetInt32():0; ViewBag.ClubId = clubId; ViewBag.Clubs = clubs; ViewBag.Events = await Get($"/api/public/clubs/{clubId}/events"); ViewBag.Agenda = await Get($"/api/public/clubs/{clubId}/agenda"); return View(); }
    public IActionResult Login() => View();
    [HttpPost] public async Task<IActionResult> Login(LoginRequest input) { if (!ModelState.IsValid) return View(); using var c = Client(); var response = await c.PostAsJsonAsync("/api/auth/login", input, JsonOptions); if (!response.IsSuccessStatusCode) { ModelState.AddModelError("", "Credenciais inválidas ou limite temporário de tentativas."); return View(); } var token = await response.Content.ReadFromJsonAsync<TokenResponse>(JsonOptions); HttpContext.Session.Clear(); HttpContext.Session.SetString("token", token!.Token); return RedirectToAction(nameof(Dashboard)); }
    [HttpPost] public IActionResult Logout() { HttpContext.Session.Clear(); return RedirectToAction(nameof(Index)); }
    public async Task<IActionResult> Register() { ViewBag.Clubs = await Get("/api/public/clubs"); return View(); }
    [HttpPost] public async Task<IActionResult> Register(RegisterRequest input) { if (ModelState.IsValid) { using var c = Client(); var response = await c.PostAsJsonAsync("/api/auth/register", input, JsonOptions); if (response.IsSuccessStatusCode) { TempData["Message"] = "Cadastro enviado. Aguarde aprovação do clube."; return RedirectToAction(nameof(Login)); } ModelState.AddModelError("", await response.Content.ReadAsStringAsync()); } ViewBag.Clubs = await Get("/api/public/clubs"); return View(input); }
    public async Task<IActionResult> Dashboard() { if (HttpContext.Session.GetString("token") == null) return RedirectToAction(nameof(Login)); try { ViewBag.Me = await Get("/api/me"); ViewBag.Clubs = await Get("/api/public/clubs"); return View(); } catch (HttpRequestException) { HttpContext.Session.Clear(); return RedirectToAction(nameof(Login)); } }
    static readonly string[] Modules = ["club", "members", "events", "reservations", "finance", "dependents", "tickets", "audit", "profile"];
    public async Task<IActionResult> Panel(int clubId, string module = "events")
    {
        if (HttpContext.Session.GetString("token") == null) return RedirectToAction(nameof(Login)); if (!Modules.Contains(module)) return NotFound();
        try
        {
            var me = await Get("/api/me"); bool super = me.GetProperty("superAdmin").GetBoolean(); var role = me.GetProperty("memberships").EnumerateArray().Where(x => x.GetProperty("clubId").GetInt32() == clubId && x.GetProperty("status").GetString() == "Aprovado").Select(x => x.GetProperty("role").GetString()).FirstOrDefault();
            var data = await Get(module == "club" ? $"/api/clubs/{clubId}" : $"/api/clubs/{clubId}/{module}"); return View(new Panel(clubId, module, data, super || role == "Presidente", super || role is "Presidente" or "Tesoureiro", super));
        }
        catch (HttpRequestException) { TempData["Message"] = "Acesso recusado ou sessão expirada. Verifique seu vínculo e perfil."; return RedirectToAction(nameof(Dashboard)); }
    }
    async Task<IActionResult> Send(int clubId, string module, string path, HttpMethod method, object body) { if (!ModelState.IsValid) { TempData["Message"] = "Revise os campos obrigatórios e formatos."; return RedirectToAction(nameof(Panel), new { clubId, module }); } using var c = Client(); using var request = new HttpRequestMessage(method, path) { Content = JsonContent.Create(body, options: JsonOptions) }; var response = await c.SendAsync(request); TempData["Message"] = response.IsSuccessStatusCode ? "Operação concluída." : $"Operação recusada ({(int)response.StatusCode}): {await response.Content.ReadAsStringAsync()}"; return RedirectToAction(nameof(Panel), new { clubId, module }); }
    [HttpPost] public Task<IActionResult> Club(int clubId, ClubInput input) => Send(clubId, "club", $"/api/clubs/{clubId}", HttpMethod.Put, input);
    [HttpPost] public async Task<IActionResult> CreateClub(ClubInput input) { if (!ModelState.IsValid) { TempData["Message"] = "Revise os campos do clube."; return RedirectToAction(nameof(Dashboard)); } using var c = Client(); var response = await c.PostAsJsonAsync("/api/clubs", input, JsonOptions); TempData["Message"] = response.IsSuccessStatusCode ? "Clube criado." : await response.Content.ReadAsStringAsync(); return RedirectToAction(nameof(Dashboard)); }
    [HttpPost] public Task<IActionResult> Approve(int clubId, int id, ApprovalInput input) => Send(clubId, "members", $"/api/clubs/{clubId}/members/{id}/approval", HttpMethod.Put, input);
    [HttpPost] public Task<IActionResult> Event(int clubId, int? id, EventInput input) { input = input with { StartUtc = DateTime.SpecifyKind(input.StartUtc, DateTimeKind.Utc), EndUtc = DateTime.SpecifyKind(input.EndUtc, DateTimeKind.Utc) }; return Send(clubId, "events", $"/api/clubs/{clubId}/events" + (id.HasValue ? $"/{id}" : ""), id.HasValue ? HttpMethod.Put : HttpMethod.Post, input); }
    [HttpPost] public Task<IActionResult> Reserve(int clubId, ReservationInput input) { input = input with { StartUtc = DateTime.SpecifyKind(input.StartUtc, DateTimeKind.Utc), EndUtc = DateTime.SpecifyKind(input.EndUtc, DateTimeKind.Utc) }; return Send(clubId, "reservations", $"/api/clubs/{clubId}/reservations", HttpMethod.Post, input); }
    [HttpPost] public Task<IActionResult> ReservationStatus(int clubId, int id, ReservationDecision input) => Send(clubId, "reservations", $"/api/clubs/{clubId}/reservations/{id}/status", HttpMethod.Put, input);
    [HttpPost] public Task<IActionResult> Charge(int clubId, ChargeInput input) => Send(clubId, "finance", $"/api/clubs/{clubId}/charges", HttpMethod.Post, input);
    [HttpPost] public Task<IActionResult> Payment(int clubId, int id, PaymentInput input) => Send(clubId, "finance", $"/api/clubs/{clubId}/charges/{id}/payment", HttpMethod.Post, input);
    [HttpPost] public Task<IActionResult> Dependent(int clubId, DependentInput input) => Send(clubId, "dependents", $"/api/clubs/{clubId}/dependents", HttpMethod.Post, input);
    [HttpPost] public Task<IActionResult> DeleteDependent(int clubId, int id) => Send(clubId, "dependents", $"/api/clubs/{clubId}/dependents/{id}", HttpMethod.Delete, new { });
    [HttpPost] public Task<IActionResult> Ticket(int clubId, TicketInput input) => Send(clubId, "tickets", $"/api/clubs/{clubId}/tickets", HttpMethod.Post, input);
    [HttpPost] public Task<IActionResult> Reply(int clubId, int id, TicketReply input) => Send(clubId, "tickets", $"/api/clubs/{clubId}/tickets/{id}", HttpMethod.Put, input);
}

