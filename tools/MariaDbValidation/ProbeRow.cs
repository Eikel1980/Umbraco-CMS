using NPoco;

/// <summary>Maps a test row to the bulk insert probe table.</summary>
[TableName("mariaDbBulkProbe")]
[PrimaryKey("id", AutoIncrement = false)]
public sealed record ProbeRow([property: Column("id")] int Id);
