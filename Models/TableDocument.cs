using System.IO;
using System.Text.Json.Serialization;

namespace TableSnap.Models;

public sealed class TableDocument
{
    public const int MaxRows = 200;
    public const int MaxColumns = 50;

    public int Rows { get; set; }
    public int Columns { get; set; }
    public List<TableCell> Cells { get; set; } = [];

    [JsonIgnore]
    public bool StructureAdjusted { get; private set; }

    public void NormalizeRecognition()
    {
        if (Cells is null)
            throw new InvalidDataException("The recognition result has no cell list.");

        foreach (var cell in Cells)
        {
            if (cell.RowSpan == 0)
            {
                cell.RowSpan = 1;
                StructureAdjusted = true;
            }
            if (cell.ColumnSpan == 0)
            {
                cell.ColumnSpan = 1;
                StructureAdjusted = true;
            }
        }

        if (Cells.Count > 0)
        {
            var lastRow = Cells.Max(cell => (long)cell.Row + cell.RowSpan);
            var lastColumn = Cells.Max(cell => (long)cell.Column + cell.ColumnSpan);

            if (Rows > 0 && Cells.Min(cell => cell.Row) == 1 && lastRow == (long)Rows + 1)
            {
                foreach (var cell in Cells) cell.Row--;
                StructureAdjusted = true;
            }
            if (Columns > 0 && Cells.Min(cell => cell.Column) == 1 && lastColumn == (long)Columns + 1)
            {
                foreach (var cell in Cells) cell.Column--;
                StructureAdjusted = true;
            }

            lastRow = Cells.Max(cell => (long)cell.Row + cell.RowSpan);
            lastColumn = Cells.Max(cell => (long)cell.Column + cell.ColumnSpan);
            if (lastRow > Rows && lastRow <= MaxRows)
            {
                Rows = (int)lastRow;
                StructureAdjusted = true;
            }
            if (lastColumn > Columns && lastColumn <= MaxColumns)
            {
                Columns = (int)lastColumn;
                StructureAdjusted = true;
            }
        }

        Validate();
    }

    public void Validate()
    {
        if (Rows is < 1 or > MaxRows || Columns is < 1 or > MaxColumns)
            throw new InvalidDataException($"Table size {Rows}x{Columns} is out of range (maximum: {MaxRows} rows and {MaxColumns} columns).");
        if (Cells is null)
            throw new InvalidDataException("The table has no cell list.");

        var occupied = new bool[Rows, Columns];
        for (var index = 0; index < Cells.Count; index++)
        {
            var cell = Cells[index];
            if (cell.Row < 0 || cell.Column < 0 || cell.RowSpan < 1 || cell.ColumnSpan < 1)
                throw new InvalidDataException($"Cell {index + 1} has invalid coordinates or span: row={cell.Row}, column={cell.Column}, rowSpan={cell.RowSpan}, columnSpan={cell.ColumnSpan}.");
            if ((long)cell.Row + cell.RowSpan > Rows || (long)cell.Column + cell.ColumnSpan > Columns)
                throw new InvalidDataException($"Cell {index + 1} at row={cell.Row}, column={cell.Column}, rowSpan={cell.RowSpan}, columnSpan={cell.ColumnSpan} exceeds the {Rows}x{Columns} table grid.");

            for (var row = cell.Row; row < cell.Row + cell.RowSpan; row++)
            for (var column = cell.Column; column < cell.Column + cell.ColumnSpan; column++)
            {
                if (occupied[row, column])
                    throw new InvalidDataException($"Cell {index + 1} overlaps another cell at row={row}, column={column}.");
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
