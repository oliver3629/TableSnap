using System.Net;
using System.Text;
using System.Windows;
using TableSnap.Models;
using DataObject = System.Windows.DataObject;
using DataFormats = System.Windows.DataFormats;
using Clipboard = System.Windows.Clipboard;

namespace TableSnap.Export;

public static class ClipboardExporter
{
    public static void Copy(TableDocument table)
    {
        table.Validate();
        var data = new DataObject();
        data.SetData(DataFormats.UnicodeText, ToTsv(table));
        data.SetData(DataFormats.CommaSeparatedValue, ToCsv(table));
        data.SetData(DataFormats.Html, ToClipboardHtml(table));
        Clipboard.SetDataObject(data, true);
    }

    private static string ToTsv(TableDocument table) =>
        string.Join("\r\n", ToMatrix(table).Select(row =>
            string.Join("\t", row.Select(value => value.Replace('\t', ' ').Replace('\r', ' ').Replace('\n', ' ')))));

    private static string ToCsv(TableDocument table) =>
        string.Join("\r\n", ToMatrix(table).Select(row =>
            string.Join(",", row.Select(value => "\"" + value.Replace("\"", "\"\"") + "\""))));

    private static string[][] ToMatrix(TableDocument table)
    {
        var rows = Enumerable.Range(0, table.Rows)
            .Select(_ => Enumerable.Repeat("", table.Columns).ToArray()).ToArray();
        foreach (var cell in table.Cells) rows[cell.Row][cell.Column] = cell.Text;
        return rows;
    }

    private static string ToClipboardHtml(TableDocument table)
    {
        var fragment = new StringBuilder("<table style=\"border-collapse:collapse\">");
        for (var row = 0; row < table.Rows; row++)
        {
            fragment.Append("<tr>");
            foreach (var cell in table.Cells.Where(cell => cell.Row == row).OrderBy(cell => cell.Column))
            {
                fragment.Append("<td");
                if (cell.RowSpan > 1) fragment.Append(" rowspan=\"").Append(cell.RowSpan).Append('"');
                if (cell.ColumnSpan > 1) fragment.Append(" colspan=\"").Append(cell.ColumnSpan).Append('"');
                fragment.Append(" style=\"border:1px solid #cccccc;text-align:")
                    .Append(cell.Align is "center" or "right" ? cell.Align : "left")
                    .Append("\">");
                if (cell.Bold) fragment.Append("<strong>");
                fragment.Append(WebUtility.HtmlEncode(cell.Text).Replace("\r\n", "<br>").Replace("\n", "<br>"));
                if (cell.Bold) fragment.Append("</strong>");
                fragment.Append("</td>");
            }
            fragment.Append("</tr>");
        }
        fragment.Append("</table>");

        const string prefix = "<html><body><!--StartFragment-->";
        const string suffix = "<!--EndFragment--></body></html>";
        const string headerTemplate = "Version:0.9\r\nStartHTML:{0:D10}\r\nEndHTML:{1:D10}\r\nStartFragment:{2:D10}\r\nEndFragment:{3:D10}\r\n";
        var emptyHeader = string.Format(headerTemplate, 0, 0, 0, 0);
        var startHtml = Encoding.UTF8.GetByteCount(emptyHeader);
        var startFragment = startHtml + Encoding.UTF8.GetByteCount(prefix);
        var endFragment = startFragment + Encoding.UTF8.GetByteCount(fragment.ToString());
        var endHtml = endFragment + Encoding.UTF8.GetByteCount(suffix);
        return string.Format(headerTemplate, startHtml, endHtml, startFragment, endFragment) +
            prefix + fragment + suffix;
    }
}
