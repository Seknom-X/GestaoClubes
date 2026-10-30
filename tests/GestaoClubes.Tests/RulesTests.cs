using GestaoClubes.Core;
using Xunit;
namespace GestaoClubes.Tests;

public class RulesTests
{
    [Theory]
    [InlineData("52998224725", true)]
    [InlineData("529.982.247-25", true)]
    [InlineData("11111111111", false)]
    [InlineData("52998224726", false)]
    [InlineData("", false)]
    public void CpfChecksDigits(string cpf, bool valid) => Assert.Equal(valid, Rules.ValidCpf(cpf));
    [Theory]
    [InlineData(MemberType.Associado, Approval.Aprovado, true)]
    [InlineData(MemberType.Fundador, Approval.Aprovado, true)]
    [InlineData(MemberType.Convidado, Approval.Aprovado, false)]
    [InlineData(MemberType.Simpatizante, Approval.Aprovado, false)]
    [InlineData(MemberType.Fundador, Approval.Pendente, false)]
    [InlineData(MemberType.Associado, Approval.Suspenso, false)]
    public void VotingRequiresApprovedVotingType(MemberType type, Approval status, bool expected) => Assert.Equal(expected, Rules.CanVote(type, status));
    [Fact] public void AdjacentReservationsDoNotOverlap() { var s = DateTime.UtcNow; Assert.False(Rules.Overlaps(s, s.AddHours(1), s.AddHours(1), s.AddHours(2))); Assert.True(Rules.Overlaps(s, s.AddHours(2), s.AddHours(1), s.AddHours(3))); Assert.True(Rules.Overlaps(s, s.AddHours(3), s.AddHours(1), s.AddHours(2))); }
    [Fact] public void ReservationIntervalMustBeUtcAndPositive() { var s = DateTime.UtcNow; Assert.False(Rules.ValidInterval(s, s)); Assert.False(Rules.ValidInterval(s, s.AddDays(32))); Assert.False(Rules.ValidInterval(DateTime.SpecifyKind(s, DateTimeKind.Unspecified), s.AddHours(1))); Assert.True(Rules.ValidInterval(s, s.AddDays(1))); }
    [Fact] public void FutureBirthRejected() => Assert.False(Rules.ValidBirth(DateOnly.FromDateTime(DateTime.UtcNow.AddDays(1))));
}
