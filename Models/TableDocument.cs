using System.IO;

namespace TableSnap.Models;

public sealed class TableDocument
{
    public int Rows { get; set; }
    public int Columns { get; set; }
    public List<TableCell> Cells { get; set; } = [];

    public void Validate()
    {
        if (Rows is < 1 or > 200 || Columns is < 1 or > 50)
            throw new InvalidDataException("Table size is out of range (maximum: 200 rows and 50 columns).");

        var occupied = new bool[Rows, Columns];
        foreach (var cell in Cells)
        {
            if (cell.Row < 0 || cell.Column < 0 || cell.RowSpan < 1 || cell.ColumnSpan < 1 ||
                cell.Row + cell.RowSpan > Rows || cell.Column + cell.ColumnSpan > Columns)
                throw new InvalidDataException("A recognized cell is outside the table bounds.");

            for (var row = cell.Row; row < cell.Row + cell.RowSpan; row++)
            for (var column = cell.Column; column < cell.Column + cell.ColumnSpan; column++)
            {
                if (occupied[row, column])
                    throw new InvalidDataException("Recognized cells overlap.");
                occupied[row, column] = true;
            }
        }
    }

    public static TableDocument Demo() => new()
    {
        Rows = 5,
        Columns = 4,
        Cells =
        [
            new() { Row = 0, Column = 0, RowSpan = 2, Text = "Method", Bold = true },
            new() { Row = 0, Column = 1, RowSpan = 2, Text = "Params", Bold = true },
            new() { Row = 0, Column = 2, ColumnSpan = 2, Text = "Score", Bold = true },
            new() { Row = 1, Column = 2, Text = "Overall", Bold = true },
            new() { Row = 1, Column = 3, Text = "TEDS", Bold = true },
            new() { Row = 2, Column = 0, Text = "Baseline" },
            new() { Row = 2, Column = 1, Text = "21M" },
            new() { Row = 2, Column = 2, Text = "91.8" },
            new() { Row = 2, Column = 3, Text = "93.1" },
            new() { Row = 3, Column = 0, Text = "StructNet" },
            new() { Row = 3, Column = 1, Text = "36M" },
            new() { Row = 3, Column = 2, Text = "95.7" },
            new() { Row = 3, Column = 3, Text = "96.4" },
            new() { Row = 4, Column = 0, Text = "Ours", Bold = true },
            new() { Row = 4, Column = 1, Text = "28M" },
            new() { Row = 4, Column = 2, Text = "96.3", Bold = true },
            new() { Row = 4, Column = 3, Text = "97.2", Bold = true }
        ]
    };
}

public sealed class TableCell
{
    public int Row { get; set; }
    public int Column { get; set; }
    public int RowSpan { get; set; } = 1;
    public int ColumnSpan { get; set; } = 1;
    public string Text { get; set; } = "";
    public bool Bold { get; set; }
    public string Align { get; set; } = "left";
}
