using System.IO.Compression;
using System.IO;
using System.Text;
using System.Xml;
using TableSnap.Models;

namespace TableSnap.Export;

public static class XlsxExporter
{
    private const string MainNs = "http://schemas.openxmlformats.org/spreadsheetml/2006/main";
    private const string RelNs = "http://schemas.openxmlformats.org/officeDocument/2006/relationships";

    public static void Save(TableDocument table, string path)
    {
        table.Validate();
        using var file = File.Create(path);
        using var zip = new ZipArchive(file, ZipArchiveMode.Create);

        AddText(zip, "[Content_Types].xml", """
            <?xml version="1.0" encoding="utf-8"?>
            <Types xmlns="http://schemas.openxmlformats.org/package/2006/content-types">
              <Default Extension="rels" ContentType="application/vnd.openxmlformats-package.relationships+xml"/>
              <Default Extension="xml" ContentType="application/xml"/>
              <Override PartName="/xl/workbook.xml" ContentType="application/vnd.openxmlformats-officedocument.spreadsheetml.sheet.main+xml"/>
              <Override PartName="/xl/worksheets/sheet1.xml" ContentType="application/vnd.openxmlformats-officedocument.spreadsheetml.worksheet+xml"/>
              <Override PartName="/xl/styles.xml" ContentType="application/vnd.openxmlformats-officedocument.spreadsheetml.styles+xml"/>
            </Types>
            """);

        AddText(zip, "_rels/.rels", """
            <?xml version="1.0" encoding="utf-8"?>
            <Relationships xmlns="http://schemas.openxmlformats.org/package/2006/relationships">
              <Relationship Id="rId1" Type="http://schemas.openxmlformats.org/officeDocument/2006/relationships/officeDocument" Target="xl/workbook.xml"/>
            </Relationships>
            """);

        AddText(zip, "xl/workbook.xml", """
            <?xml version="1.0" encoding="utf-8"?>
            <workbook xmlns="http://schemas.openxmlformats.org/spreadsheetml/2006/main" xmlns:r="http://schemas.openxmlformats.org/officeDocument/2006/relationships">
              <sheets><sheet name="Table" sheetId="1" r:id="rId1"/></sheets>
            </workbook>
            """);

        AddText(zip, "xl/_rels/workbook.xml.rels", """
            <?xml version="1.0" encoding="utf-8"?>
            <Relationships xmlns="http://schemas.openxmlformats.org/package/2006/relationships">
              <Relationship Id="rId1" Type="http://schemas.openxmlformats.org/officeDocument/2006/relationships/worksheet" Target="worksheets/sheet1.xml"/>
              <Relationship Id="rId2" Type="http://schemas.openxmlformats.org/officeDocument/2006/relationships/styles" Target="styles.xml"/>
            </Relationships>
            """);

        AddText(zip, "xl/styles.xml", """
            <?xml version="1.0" encoding="utf-8"?>
            <styleSheet xmlns="http://schemas.openxmlformats.org/spreadsheetml/2006/main">
              <fonts count="2"><font><sz val="11"/><name val="Aptos"/></font><font><b/><sz val="11"/><name val="Aptos"/></font></fonts>
              <fills count="2"><fill><patternFill patternType="none"/></fill><fill><patternFill patternType="gray125"/></fill></fills>
              <borders count="2"><border><left/><right/><top/><bottom/><diagonal/></border><border><left style="thin"><color rgb="FFD9DEE5"/></left><right style="thin"><color rgb="FFD9DEE5"/></right><top style="thin"><color rgb="FFD9DEE5"/></top><bottom style="thin"><color rgb="FFD9DEE5"/></bottom><diagonal/></border></borders>
              <cellStyleXfs count="1"><xf numFmtId="0" fontId="0" fillId="0" borderId="0"/></cellStyleXfs>
              <cellXfs count="6">
                <xf numFmtId="0" fontId="0" fillId="0" borderId="1" xfId="0" applyAlignment="1"><alignment horizontal="left" vertical="center" wrapText="1"/></xf>
                <xf numFmtId="0" fontId="0" fillId="0" borderId="1" xfId="0" applyAlignment="1"><alignment horizontal="center" vertical="center" wrapText="1"/></xf>
                <xf numFmtId="0" fontId="0" fillId="0" borderId="1" xfId="0" applyAlignment="1"><alignment horizontal="right" vertical="center" wrapText="1"/></xf>
                <xf numFmtId="0" fontId="1" fillId="0" borderId="1" xfId="0" applyFont="1" applyAlignment="1"><alignment horizontal="left" vertical="center" wrapText="1"/></xf>
                <xf numFmtId="0" fontId="1" fillId="0" borderId="1" xfId="0" applyFont="1" applyAlignment="1"><alignment horizontal="center" vertical="center" wrapText="1"/></xf>
                <xf numFmtId="0" fontId="1" fillId="0" borderId="1" xfId="0" applyFont="1" applyAlignment="1"><alignment horizontal="right" vertical="center" wrapText="1"/></xf>
              </cellXfs>
              <cellStyles count="1"><cellStyle name="Normal" xfId="0" builtinId="0"/></cellStyles>
            </styleSheet>
            """);

        var sheetEntry = zip.CreateEntry("xl/worksheets/sheet1.xml", CompressionLevel.Optimal);
        using var sheetStream = sheetEntry.Open();
        using var xml = XmlWriter.Create(sheetStream, new XmlWriterSettings
        {
            Encoding = new UTF8Encoding(false),
            Indent = false,
            CloseOutput = false
        });
        xml.WriteStartDocument();
        xml.WriteStartElement("worksheet", MainNs);
        xml.WriteAttributeString("xmlns", "r", null, RelNs);

        xml.WriteStartElement("cols", MainNs);
        for (var column = 1; column <= table.Columns; column++)
        {
            xml.WriteStartElement("col", MainNs);
            xml.WriteAttributeString("min", column.ToString());
            xml.WriteAttributeString("max", column.ToString());
            xml.WriteAttributeString("width", "20");
            xml.WriteAttributeString("customWidth", "1");
            xml.WriteEndElement();
        }
        xml.WriteEndElement();

        xml.WriteStartElement("sheetData", MainNs);
        for (var row = 0; row < table.Rows; row++)
        {
            xml.WriteStartElement("row", MainNs);
            xml.WriteAttributeString("r", (row + 1).ToString());
            xml.WriteAttributeString("ht", "28");
            xml.WriteAttributeString("customHeight", "1");
            foreach (var cell in table.Cells.Where(cell => cell.Row == row).OrderBy(cell => cell.Column))
            {
                xml.WriteStartElement("c", MainNs);
                xml.WriteAttributeString("r", CellRef(cell.Row, cell.Column));
                xml.WriteAttributeString("s", StyleIndex(cell).ToString());
                xml.WriteAttributeString("t", "inlineStr");
                xml.WriteStartElement("is", MainNs);
                xml.WriteStartElement("t", MainNs);
                xml.WriteAttributeString("xml", "space", "http://www.w3.org/XML/1998/namespace", "preserve");
                xml.WriteString(cell.Text ?? "");
                xml.WriteEndElement();
                xml.WriteEndElement();
                xml.WriteEndElement();
            }
            xml.WriteEndElement();
        }
        xml.WriteEndElement();

        var merged = table.Cells.Where(cell => cell.RowSpan > 1 || cell.ColumnSpan > 1).ToList();
        if (merged.Count > 0)
        {
            xml.WriteStartElement("mergeCells", MainNs);
            xml.WriteAttributeString("count", merged.Count.ToString());
            foreach (var cell in merged)
            {
                xml.WriteStartElement("mergeCell", MainNs);
                xml.WriteAttributeString("ref", CellRef(cell.Row, cell.Column) + ":" +
                    CellRef(cell.Row + cell.RowSpan - 1, cell.Column + cell.ColumnSpan - 1));
                xml.WriteEndElement();
            }
            xml.WriteEndElement();
        }

        xml.WriteEndElement();
        xml.WriteEndDocument();
    }

    private static int StyleIndex(TableCell cell) =>
        (cell.Bold ? 3 : 0) + (cell.Align == "center" ? 1 : cell.Align == "right" ? 2 : 0);

    private static string CellRef(int zeroBasedRow, int zeroBasedColumn)
    {
        var column = zeroBasedColumn + 1;
        var letters = "";
        while (column > 0)
        {
            column--;
            letters = (char)('A' + column % 26) + letters;
            column /= 26;
        }
        return letters + (zeroBasedRow + 1);
    }

    private static void AddText(ZipArchive zip, string name, string content)
    {
        var entry = zip.CreateEntry(name, CompressionLevel.Optimal);
        using var writer = new StreamWriter(entry.Open(), new UTF8Encoding(false));
        writer.Write(content);
    }
}
