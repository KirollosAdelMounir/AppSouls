using TicketingCRM.Infrastructure.Administration;
using Xunit;
namespace TicketingCRM.Infrastructure.Tests;
public sealed class SetupFailuresTests
{
    [Theory]
    [InlineData("28P01","database-authentication")]
    [InlineData("3D000","database-missing")]
    [InlineData("42501","database-permissions")]
    public void ProviderErrorsUseFixedMessagesWithoutSecretDetails(string sqlState,string code)
    {
        var error=new Npgsql.PostgresException("private-password", "ERROR","ERROR",sqlState,detail:"secret-connection-string");
        var result=SetupFailures.Classify(error);Assert.Equal(code,result.Code);Assert.DoesNotContain("private-password",result.Message);Assert.DoesNotContain("secret-connection-string",result.Message);
    }
    [Fact]
    public void UnknownValidationErrorsNeverExposeTheirMessage()
    {
        var result=SetupFailures.Classify(new InvalidOperationException("Password=private-secret"));
        Assert.Equal("activation",result.Code);Assert.DoesNotContain("private-secret",result.Message);
    }
    [Fact]
    public void ExecutionStrategyWrapperPreservesProviderFailureCategoryWithoutExposingMessages()
    {
        var provider=new Npgsql.PostgresException("private-password", "FATAL","FATAL","28P01");
        var wrapped=new InvalidOperationException("private connection string",provider);
        var result=SetupFailures.Classify(wrapped);
        Assert.Equal("database-authentication",result.Code);Assert.DoesNotContain("private",result.Message);
        var nested=new InvalidOperationException("secret",new TimeoutException("private host"));
        Assert.Equal("timeout",SetupFailures.Classify(nested).Code);
    }

}
