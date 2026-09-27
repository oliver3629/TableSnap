using System.IO;
using System.Text.Json;
using TableSnap.Models;

Run("zero spans become single cells", () =>
{
    var table = new TableDocument
    {
        Rows = 1,
        Columns = 1,
        Cells = [new TableCell { RowSpan = 0, ColumnSpan = 0, Text = "value" }]
    };
    table.NormalizeRecognition();
    Check(table.Cells[0].RowSpan == 1 && table.Cells[0].ColumnSpan == 1);
    Check(table.StructureAdjusted);
});

Run("camel-case recognition JSON is normalized", () =>
{
    const string json = """
        {"rows":1,"columns":1,"cells":[{"row":0,"column":0,"rowSpan":0,"columnSpan":0,"text":"value","bold":false,"align":"left"}]}
        """;
    var table = JsonSerializer.Deserialize<TableDocument>(json,
        new JsonSerializerOptions { PropertyNameCaseInsensitive = true })!;
    table.NormalizeRecognition();
    Check(table.Cells[0].Text == "value");
    Check(table.Cells[0].RowSpan == 1 && table.Cells[0].ColumnSpan == 1);
});

Run("underreported dimensions expand to fit zero-based cells", () =>
{
    var table = new TableDocument
    {
        Rows = 2,
        Columns = 2,
        Cells =
        [
            new TableCell { Row = 0, Column = 0 },
            new TableCell { Row = 2, Column = 2 }
        ]
    };
    table.NormalizeRecognition();
    Check(table.Rows == 3 && table.Columns == 3);
    Check(table.Cells[1].Row == 2 && table.Cells[1].Column == 2);
});

Run("one-based coordinates shift to zero-based", () =>
{
    var table = new TableDocument
    {
        Rows = 2,
        Columns = 2,
        Cells =
        [
            new TableCell { Row = 1, Column = 1 },
            new TableCell { Row = 2, Column = 2 }
        ]
    };
    table.NormalizeRecognition();
    Check(table.Rows == 2 && table.Columns == 2);
    Check(table.Cells[0].Row == 0 && table.Cells[0].Column == 0);
    Check(table.Cells[1].Row == 1 && table.Cells[1].Column == 1);
});

Run("merged header spans remain intact", () =>
{
    var table = TableDocument.Demo();
    table.NormalizeRecognition();
    Check(!table.StructureAdjusted);
    Check(table.Cells[0].RowSpan == 2);
    Check(table.Cells[2].ColumnSpan == 2);
});

Run("overlapping cells are rejected", () =>
{
    var table = new TableDocument
    {
        Rows = 1,
        Columns = 1,
        Cells = [new TableCell(), new TableCell()]
    };
    ExpectInvalid(table, "overlaps another cell");
});

Run("oversized coordinates are rejected with a cell number", () =>
{
    var table = new TableDocument
    {
        Rows = 1,
        Columns = 1,
        Cells = [new TableCell { Row = 1000 }]
    };
    ExpectInvalid(table, "Cell 1");
});

Run("negative spans are rejected with details", () =>
{
    var table = new TableDocument
    {
        Rows = 1,
        Columns = 1,
        Cells = [new TableCell { RowSpan = -1 }]
    };
    ExpectInvalid(table, "rowSpan=-1");
});

Console.WriteLine("All table structure tests passed.");

static void Run(string name, Action test)
{
    test();
    Console.WriteLine("PASS " + name);
}

static void Check(bool condition)
{
    if (!condition) throw new Exception("Test assertion failed.");
}

static void ExpectInvalid(TableDocument table, string messagePart)
{
    try
    {
        table.NormalizeRecognition();
    }
    catch (InvalidDataException ex) when (ex.Message.Contains(messagePart, StringComparison.Ordinal))
    {
        return;
    }
    throw new Exception("Expected a validation error containing: " + messagePart);
}
