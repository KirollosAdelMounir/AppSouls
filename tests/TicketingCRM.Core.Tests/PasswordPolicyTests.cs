using TicketingCRM.Core.Security;
using Xunit;

namespace TicketingCRM.Core.Tests;

public sealed class PasswordPolicyTests
{
    [Theory]
    [InlineData(8)]
    [InlineData(1024)]
    public void ValidPasswordAcceptsInclusiveLengthBoundaries(int length)
    {
        PasswordPolicy.Validate("Aa1!" + new string('x', length - 4));
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("Aa1!xyz")]
    [InlineData("aa1!xyzz")]
    [InlineData("AA1!XYZZ")]
    [InlineData("Aaa!xyzz")]
    [InlineData("Aaa1xyzz")]
    public void InvalidPasswordsAreRejectedWithoutReturningThePassword(string? password)
    {
        var error = Assert.Throws<ArgumentException>(() => PasswordPolicy.Validate(password));
        Assert.Equal("Use 8–1024 characters with uppercase, lowercase, numbers and symbols.", error.Message);
    }

    [Fact]
    public void PasswordBeyondMaximumLengthIsRejected()
    {
        Assert.Throws<ArgumentException>(() => PasswordPolicy.Validate("Aa1!" + new string('x', 1021)));
    }
}
