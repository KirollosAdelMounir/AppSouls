using System.IO.Compression;
using System.Xml;
namespace TicketingCRM.Core.FileProcessing;

/// <summary>Streaming XLSX cells are literal strings, including user answers beginning with formula characters.</summary>
public static class SpreadsheetWriter
{
    public static async Task<Stream> WriteAsync(IAsyncEnumerable<IReadOnlyList<string>> rows, CancellationToken ct = default, IReadOnlySet<int>? numericColumns = null)
    {
        var path = Path.Combine(Path.GetTempPath(), $"appsouls-export-{Guid.NewGuid():N}.xlsx");
        var options = new FileStreamOptions { Mode = FileMode.CreateNew, Access = FileAccess.ReadWrite, Share = FileShare.Read, Options = FileOptions.Asynchronous | FileOptions.DeleteOnClose };
        if (!OperatingSystem.IsWindows()) options.UnixCreateMode = UnixFileMode.UserRead | UnixFileMode.UserWrite;
        var output = new FileStream(path, options);
        try
        {
            using (var zip = new ZipArchive(output, ZipArchiveMode.Create, true))
            {
                Write(zip, "[Content_Types].xml", "<Types xmlns=\"http://schemas.openxmlformats.org/package/2006/content-types\"><Default Extension=\"rels\" ContentType=\"application/vnd.openxmlformats-package.relationships+xml\"/><Default Extension=\"xml\" ContentType=\"application/xml\"/><Override PartName=\"/xl/workbook.xml\" ContentType=\"application/vnd.openxmlformats-officedocument.spreadsheetml.sheet.main+xml\"/><Override PartName=\"/xl/worksheets/sheet1.xml\" ContentType=\"application/vnd.openxmlformats-officedocument.spreadsheetml.worksheet+xml\"/></Types>");
                Write(zip, "_rels/.rels", "<Relationships xmlns=\"http://schemas.openxmlformats.org/package/2006/relationships\"><Relationship Id=\"rId1\" Type=\"http://schemas.openxmlformats.org/officeDocument/2006/relationships/officeDocument\" Target=\"xl/workbook.xml\"/></Relationships>");
                Write(zip, "xl/workbook.xml", "<workbook xmlns=\"http://schemas.openxmlformats.org/spreadsheetml/2006/main\" xmlns:r=\"http://schemas.openxmlformats.org/officeDocument/2006/relationships\"><sheets><sheet name=\"Responses\" sheetId=\"1\" r:id=\"rId1\"/></sheets></workbook>");
                Write(zip, "xl/_rels/workbook.xml.rels", "<Relationships xmlns=\"http://schemas.openxmlformats.org/package/2006/relationships\"><Relationship Id=\"rId1\" Type=\"http://schemas.openxmlformats.org/officeDocument/2006/relationships/worksheet\" Target=\"worksheets/sheet1.xml\"/></Relationships>");
                using var sheet = zip.CreateEntry("xl/worksheets/sheet1.xml").Open();
                using var xml = XmlWriter.Create(sheet, new XmlWriterSettings { Async = true, Encoding = new System.Text.UTF8Encoding(false), CloseOutput = false });
                xml.WriteStartElement("worksheet", "http://schemas.openxmlformats.org/spreadsheetml/2006/main"); xml.WriteStartElement("sheetData");
                var rowIndex=0;
                await foreach (var row in rows.WithCancellation(ct))
                {
                    ct.ThrowIfCancellationRequested(); xml.WriteStartElement("row");
                    var header=rowIndex++==0;var column=0;
                    foreach (var cell in row)
                    {
                        xml.WriteStartElement("c");
                        if(!header && numericColumns?.Contains(column)==true && decimal.TryParse(cell,System.Globalization.NumberStyles.AllowLeadingSign|System.Globalization.NumberStyles.AllowDecimalPoint,System.Globalization.CultureInfo.InvariantCulture,out var number))
                        { xml.WriteAttributeString("t","n");xml.WriteElementString("v",number.ToString(System.Globalization.CultureInfo.InvariantCulture)); }
                        else
                        { xml.WriteAttributeString("t", "inlineStr"); xml.WriteStartElement("is"); xml.WriteStartElement("t"); xml.WriteAttributeString("xml", "space", null, "preserve"); xml.WriteString(new string((cell ?? "").Where(XmlConvert.IsXmlChar).Take(32767).ToArray())); xml.WriteEndElement(); xml.WriteEndElement(); }
                        xml.WriteEndElement();column++;
                    }
                    xml.WriteEndElement(); await xml.FlushAsync();
                }
                xml.WriteEndElement(); xml.WriteEndElement(); await xml.FlushAsync();
            }
            output.Position = 0; return output;
        }
        catch { await output.DisposeAsync(); throw; }
    }
    private static void Write(ZipArchive zip, string name, string content) { using var writer = new StreamWriter(zip.CreateEntry(name).Open(), new System.Text.UTF8Encoding(false)); writer.Write(content); }
}
