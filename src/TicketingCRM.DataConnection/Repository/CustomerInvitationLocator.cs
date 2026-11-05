using System.Data;
using System.Data.Common;
using Microsoft.Data.SqlClient;
using Npgsql;
using TicketingCRM.DataConnection.Configuration;
namespace TicketingCRM.DataConnection.Repository;
/// <summary>Pre-authentication locator only. A returned tenant ID grants no authority without invitation proof.</summary>
public static class CustomerInvitationLocator
{
 public static async Task<(Guid TenantId,string TokenHash)?> LocateAsync(DatabaseProvider provider,string connectionString,Guid id,CancellationToken ct)
 {
  await using DbConnection connection=provider==DatabaseProvider.PostgreSql?new NpgsqlConnection(connectionString):new SqlConnection(connectionString);
  await connection.OpenAsync(ct);await using var command=connection.CreateCommand();command.CommandTimeout=10;
  command.CommandText=provider==DatabaseProvider.PostgreSql?"SELECT \"TenantId\", \"TokenHash\" FROM \"CustomerInvitations\" WHERE \"Id\" = @id":"SELECT [TenantId], [TokenHash] FROM [CustomerInvitations] WHERE [Id] = @id";
  var parameter=command.CreateParameter();parameter.ParameterName="@id";parameter.DbType=DbType.Guid;parameter.Value=id;command.Parameters.Add(parameter);
  await using var reader=await command.ExecuteReaderAsync(CommandBehavior.SingleRow,ct);
  return await reader.ReadAsync(ct)?(reader.GetGuid(0),reader.GetString(1)):null;
 }
}
