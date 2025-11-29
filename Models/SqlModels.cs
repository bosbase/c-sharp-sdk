namespace Bosbase.Models;

public record SqlExecuteResponse(
    List<string>? Columns = null,
    List<List<string>>? Rows = null,
    int? RowsAffected = null)
{
    public static SqlExecuteResponse FromDictionary(IDictionary<string, object?>? data)
    {
        var columns = new List<string>();
        var rows = new List<List<string>>();
        int? rowsAffected = null;

        if (data != null)
        {
            if (data.TryGetValue("columns", out var colsObj) && colsObj is IEnumerable<object?> cols)
            {
                columns.AddRange(cols.Select(c => c?.ToString() ?? string.Empty));
            }

            if (data.TryGetValue("rows", out var rowsObj) && rowsObj is IEnumerable<object?> rowList)
            {
                foreach (var row in rowList)
                {
                    if (row is IEnumerable<object?> values)
                    {
                        rows.Add(values.Select(v => v?.ToString() ?? string.Empty).ToList());
                    }
                }
            }

            if (data.TryGetValue("rowsAffected", out var raObj) && int.TryParse(raObj?.ToString(), out var ra))
            {
                rowsAffected = ra;
            }
        }

        return new SqlExecuteResponse(
            Columns: columns.Any() ? columns : null,
            Rows: rows.Any() ? rows : null,
            RowsAffected: rowsAffected);
    }
}

public record SqlTableDefinition(string Name, string? Sql = null)
{
    public Dictionary<string, object?> ToDictionary()
    {
        var payload = new Dictionary<string, object?>
        {
            ["name"] = Name
        };
        if (!string.IsNullOrWhiteSpace(Sql))
        {
            payload["sql"] = Sql;
        }

        return payload;
    }
}

public record SqlTableImportResult(
    List<Dictionary<string, object?>> Created,
    List<string> Skipped)
{
    public static SqlTableImportResult FromDictionary(IDictionary<string, object?>? data)
    {
        var created = new List<Dictionary<string, object?>>();
        var skipped = new List<string>();

        if (data != null)
        {
            if (data.TryGetValue("created", out var createdObj) && createdObj is IEnumerable<object?> createdList)
            {
                foreach (var item in createdList)
                {
                    if (item is Dictionary<string, object?> dict)
                    {
                        created.Add(dict);
                    }
                }
            }

            if (data.TryGetValue("skipped", out var skippedObj) && skippedObj is IEnumerable<object?> skippedList)
            {
                foreach (var item in skippedList)
                {
                    var value = item?.ToString();
                    if (!string.IsNullOrWhiteSpace(value))
                    {
                        skipped.Add(value);
                    }
                }
            }
        }

        return new SqlTableImportResult(created, skipped);
    }
}
