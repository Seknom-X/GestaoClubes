using System.ComponentModel.DataAnnotations;
namespace GestaoClubes.Core;

public enum Role { Presidente, Tesoureiro, Associado, Convidado, Simpatizante }
public enum MemberType { Associado, Fundador, Convidado, Simpatizante }
public enum Approval { Pendente, Aprovado, Rejeitado, Suspenso }
public enum ReservationStatus { Pendente, Aprovada, Rejeitada, Cancelada }
public enum TicketStatus { Aberta, EmAtendimento, Resolvida }
public record LoginRequest([Required, EmailAddress] string Email, [Required] string Password);
public record TokenResponse(string Token, DateTime ExpiresUtc);
public record RegisterRequest([Range(1, int.MaxValue)] int ClubId, [Required, StringLength(120, MinimumLength = 3)] string Name,
 [Required, EmailAddress, StringLength(160)] string Email, [Required, StringLength(100, MinimumLength = 12)] string Password,
 MemberType Type, [Required] string Cpf, DateOnly BirthDate, [Required, StringLength(200)] string Address,
 [Required, StringLength(80)] string Neighborhood, [Required, StringLength(80)] string City,
 [Required, RegularExpression("^[A-Z]{2}$")] string State, [StringLength(30)] string Phone,
 [StringLength(40)] string Sex, [StringLength(40)] string MaritalStatus, [StringLength(300)] string Interests, bool PrivacyAccepted);
public record ClubInput([Required, StringLength(120)] string Name, [Required, StringLength(12)] string Acronym,
 [Required, StringLength(200)] string Address, [Required, StringLength(80)] string Neighborhood,
 [Required, StringLength(80)] string City, [Required, RegularExpression("^[A-Z]{2}$")] string State,
 [StringLength(30)] string Phone, [Required, EmailAddress, StringLength(160)] string Email,
 [Range(typeof(decimal), "0", "100000")] decimal MonthlyFee);
public record ApprovalInput(Approval Status, Role Role, MemberType Type);
public record DependentInput([Required, StringLength(120)] string Name, DateOnly BirthDate, [Required, StringLength(40)] string Relationship);
public record EventInput([Required, StringLength(160)] string Title, DateTime StartUtc, DateTime EndUtc, bool Public, bool Cancelled);
public record ReservationInput([Required, StringLength(80)] string Space, [Required, StringLength(160)] string Purpose, DateTime StartUtc, DateTime EndUtc);
public record ReservationDecision(ReservationStatus Status);
public record ChargeInput([Range(1, int.MaxValue)] int MembershipId, [RegularExpression("^20[0-9]{2}-(0[1-9]|1[0-2])$")] string Period, DateOnly DueDate);
public record PaymentInput([Required, StringLength(200)] string Reference);
public record TicketInput([Required, RegularExpression("^(Solicitacao|Reclamacao)$")] string Kind, [Required, StringLength(2000)] string Text);
public record TicketReply(TicketStatus Status, [Required, StringLength(2000)] string Response);
public static class Rules
{
    public static bool CanVote(MemberType type, Approval status) => status == Approval.Aprovado && type is MemberType.Associado or MemberType.Fundador;
    public static bool Overlaps(DateTime start, DateTime end, DateTime otherStart, DateTime otherEnd) => start < otherEnd && otherStart < end;
    public static bool ValidInterval(DateTime start, DateTime end) => start.Kind == DateTimeKind.Utc && end.Kind == DateTimeKind.Utc && end > start && end - start <= TimeSpan.FromDays(31);
    public static bool ValidCpf(string? value)
    {
        if (value is null || value.Length > 14 || value.Any(c => !char.IsAsciiDigit(c) && c != '.' && c != '-' && c != ' ')) return false;
        var cpf = new string(value.Where(char.IsAsciiDigit).ToArray());
        if (cpf.Length != 11 || cpf.Distinct().Count() == 1) return false;
        for (int n = 9; n <= 10; n++) { int sum = 0; for (int i = 0; i < n; i++) sum += (cpf[i] - '0') * (n + 1 - i); int check = (sum * 10) % 11; if (check == 10) check = 0; if (check != cpf[n] - '0') return false; }
        return true;
    }
    public static bool ValidBirth(DateOnly date) => date <= DateOnly.FromDateTime(DateTime.UtcNow) && date >= DateOnly.FromDateTime(DateTime.UtcNow.AddYears(-120));
}
