using System.Text.Json;
namespace GestaoClubes.Web.Models;

public record Panel(int ClubId, string Module, JsonElement Data, bool Admin, bool Treasury, bool SuperAdmin);
