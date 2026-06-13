namespace LogSeqDBExport;

/// <summary>
/// Class to represent a source entity extracted from the database, with its
/// ID, property name, value and the associated transaction.
/// </summary>
public record SourceEntity(double Id, string Name, object? Value, long Transaction);
