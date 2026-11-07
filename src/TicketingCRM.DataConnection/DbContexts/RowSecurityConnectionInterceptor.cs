using System.Data;
using System.Data.Common;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.Data.SqlClient;
using Npgsql;
namespace TicketingCRM.DataConnection.DbContexts;

/// <summary>Business contexts enter a restricted principal; narrow directory/bootstrap contexts keep administrative scope.</summary>
internal sealed class RowSecurityConnectionInterceptor:DbConnectionInterceptor
{
 internal static readonly RowSecurityConnectionInterceptor Instance=new();
 private static DbCommand ScopeCommand(DbConnection connection,AppDbContext context,bool reset)
 {
  var cmd=connection.CreateCommand();cmd.CommandTimeout=10;
  if(connection is NpgsqlConnection)
   cmd.CommandText=reset?"RESET ROLE; SELECT set_config('appsouls.tenant_id','',false),set_config('appsouls.customer_id','',false),set_config('appsouls.public_form_id','',false);":"SET ROLE appsouls_runtime; SELECT set_config('appsouls.tenant_id',@tenant,false),set_config('appsouls.customer_id',@customer,false),set_config('appsouls.public_form_id',@form,false);";
  else if(connection is SqlConnection)
   cmd.CommandText=reset?"REVERT; EXEC sys.sp_set_session_context @key=N'appsouls_tenant',@value=NULL; EXEC sys.sp_set_session_context @key=N'appsouls_customer',@value=NULL; EXEC sys.sp_set_session_context @key=N'appsouls_public_form',@value=NULL;":"EXECUTE AS USER=N'appsouls_runtime'; EXEC sys.sp_set_session_context @key=N'appsouls_tenant',@value=@tenant; EXEC sys.sp_set_session_context @key=N'appsouls_customer',@value=@customer; EXEC sys.sp_set_session_context @key=N'appsouls_public_form',@value=@form;";
  else throw new NotSupportedException("The selected database provider has no row-security session implementation.");
  if(!reset)
  {
   foreach(var pair in new[]{("tenant",context.RowTenant.ToString("D")),("customer",context.RowCustomer?.ToString("D")??""),("form",context.RowPublicForm?.ToString("D")??"")})
   {var parameter=cmd.CreateParameter();parameter.ParameterName=pair.Item1;parameter.DbType=DbType.String;parameter.Value=pair.Item2;cmd.Parameters.Add(parameter);}
  }
  return cmd;
 }
 private static void ClearPool(DbConnection connection){if(connection is NpgsqlConnection postgres)NpgsqlConnection.ClearPool(postgres);else if(connection is SqlConnection sql)SqlConnection.ClearPool(sql);}
 public override void ConnectionOpened(DbConnection connection,ConnectionEndEventData eventData)
 {
  if(eventData.Context is not AppDbContext context)return;
  try{using var cmd=ScopeCommand(connection,context,false);cmd.ExecuteNonQuery();context.RowPrincipalActive=true;}catch{ClearPool(connection);throw;}
 }
 public override async Task ConnectionOpenedAsync(DbConnection connection,ConnectionEndEventData eventData,CancellationToken cancellationToken=default)
 {
  if(eventData.Context is not AppDbContext context)return;
  try{await using var cmd=ScopeCommand(connection,context,false);await cmd.ExecuteNonQueryAsync(cancellationToken);context.RowPrincipalActive=true;}catch{ClearPool(connection);throw;}
 }
 public override InterceptionResult ConnectionClosing(DbConnection connection,ConnectionEventData eventData,InterceptionResult result)
 {
  if(eventData.Context is AppDbContext context&&context.RowPrincipalActive)
  {try{using var cmd=ScopeCommand(connection,context,true);cmd.ExecuteNonQuery();}catch{ClearPool(connection);}finally{context.RowPrincipalActive=false;}}
  return result;
 }
 public override async ValueTask<InterceptionResult> ConnectionClosingAsync(DbConnection connection,ConnectionEventData eventData,InterceptionResult result)
 {
  if(eventData.Context is AppDbContext context&&context.RowPrincipalActive)
  {try{await using var cmd=ScopeCommand(connection,context,true);await cmd.ExecuteNonQueryAsync(CancellationToken.None);}catch{ClearPool(connection);}finally{context.RowPrincipalActive=false;}}
  return result;
 }
}
internal interface IPublicFormRowScope { Guid FormId {get;} }
