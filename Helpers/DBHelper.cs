using LogSeqDBExport.Models;
using Microsoft.Data.Sqlite;

namespace LogSeqDBExport.Helpers;

internal static class DBHelper
{
    internal static string ReadFromDb(Options opt)
    {
        string tmpjson;

        using var conn = new SqliteConnection($"Data Source={opt.DbPath}");
        conn.Open();

        var sql = opt.Query ?? $"select content from {opt.Table}";
        var cmd = conn.CreateCommand();
        cmd.CommandText = sql;
        var reader = cmd.ExecuteReader();
        var sbx = new List<string>(100_000);

        while (reader.Read())
        {
            sbx.Add($"\t{{\n\t\"content\": \"{reader.GetString(0).Replace("\\", "\\\\").Replace("\"", "\\\"")}\"\n\t}}");
        }

        tmpjson = $"[{String.Join(",", sbx)}]";

        return tmpjson;
    }
}