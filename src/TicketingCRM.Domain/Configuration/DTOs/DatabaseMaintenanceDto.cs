namespace TicketingCRM.Domain.Configuration.DTOs;

public sealed class DatabaseMaintenanceRequest
{
    public string Provider { get; set; } = "PostgreSql";
    public string ConnectionString { get; set; } = "";
    public string DatabaseServer { get; set; } = "";
    public int DatabasePort { get; set; } = 5432;
    public string DatabaseCatalog { get; set; } = "";
    public string DatabaseUserName { get; set; } = "";
    public string? DatabasePassword { get; set; }
    public bool DatabaseUseTls { get; set; } = true;
    public string Mode { get; set; } = "Switch";
    public bool ConfirmMaintenance { get; set; }
}

public sealed record DatabaseInspection(string State, int AppliedMigrations, int PendingMigrations, bool HasAccounts);
public sealed record DatabaseTransferProgress(string Stage, double? Percentage, string? Table = null,
    long RowsCopied = 0, long TotalRows = 0, double RowsPerSecond = 0, double ElapsedSeconds = 0,
    string? Message = null, bool Finished = false, bool Success = false, IReadOnlyList<DatabaseStageEvent>? Stages = null,
    long BytesCopied = 0, long TotalBytes = 0, double BytesPerSecond = 0, string? Source = null, string? Target = null, string? Mode = null);
public sealed record DatabaseStageEvent(DateTimeOffset AtUtc, string Stage, string? Table);
