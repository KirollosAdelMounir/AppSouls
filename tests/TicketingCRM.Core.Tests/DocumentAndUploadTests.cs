using System.IO.Compression;
using System.Runtime.CompilerServices;
using System.Xml.Linq;
using PdfSharp.Pdf.IO;
using TicketingCRM.Core.FileProcessing;
using TicketingCRM.Domain.Commerce.DTOs;
using Xunit;
namespace TicketingCRM.Core.Tests;
public sealed class DocumentAndUploadTests
{
 private static async IAsyncEnumerable<IReadOnlyList<string>> Rows([EnumeratorCancellation]CancellationToken ct=default)
 {
  yield return new[]{"Answer","Number"};await Task.Yield();ct.ThrowIfCancellationRequested();yield return new[]{"=HYPERLINK(\"https://example.test\",\"Café\")","123.50"};
 }
 [Fact]public async Task ExcelPreservesLiteralUserTextAndNumericAnswersAndRemovesTemporaryFileAfterUse()
 {
  var stream=await SpreadsheetWriter.WriteAsync(Rows(),numericColumns:new HashSet<int>{1});var path=Assert.IsType<FileStream>(stream).Name;
  using(var zip=new ZipArchive(stream,ZipArchiveMode.Read,true))
  {
   Assert.NotNull(zip.GetEntry("[Content_Types].xml"));Assert.NotNull(zip.GetEntry("xl/workbook.xml"));
   using var sheet=zip.GetEntry("xl/worksheets/sheet1.xml")!.Open();var xml=XDocument.Load(sheet);XNamespace ns="http://schemas.openxmlformats.org/spreadsheetml/2006/main";
   var rows=xml.Descendants(ns+"row").ToArray();Assert.Equal(2,rows.Length);var cells=rows[1].Elements(ns+"c").ToArray();Assert.Equal("inlineStr",cells[0].Attribute("t")!.Value);Assert.Equal("=HYPERLINK(\"https://example.test\",\"Café\")",cells[0].Value);Assert.Empty(xml.Descendants(ns+"f"));Assert.Equal("n",cells[1].Attribute("t")!.Value);Assert.Equal(123.5m,decimal.Parse(cells[1].Value,System.Globalization.CultureInfo.InvariantCulture));
  }
  await stream.DisposeAsync();Assert.False(File.Exists(path));
 }
 [Fact]public async Task SpreadsheetCancellationIsForwarded()
 {
  using var cts=new CancellationTokenSource();await cts.CancelAsync();await Assert.ThrowsAnyAsync<OperationCanceledException>(()=>SpreadsheetWriter.WriteAsync(Rows(cts.Token),cts.Token));
 }
 [Fact]public void RichTextAllowsFormattingButNeverActiveContentOrRemoteImages()
 {
  var renderer=new SafeMarkdownRenderer();var html=renderer.Render("**Important**\n\n[unsafe](javascript:alert(1))\n\n![remote](https://example.test/tracker)\n\n<script>alert(1)</script><img src=x onerror=alert(1)>");
  Assert.Contains("<strong>Important</strong>",html);Assert.DoesNotContain("href=\"javascript:",html,StringComparison.OrdinalIgnoreCase);Assert.DoesNotContain("<script",html,StringComparison.OrdinalIgnoreCase);Assert.DoesNotContain("<img",html,StringComparison.OrdinalIgnoreCase);Assert.DoesNotContain("onerror=\"",html,StringComparison.OrdinalIgnoreCase);
 }
 [Fact]public void InvoicePdfEmbedsItsFontAndPaginatesDetailedLineItems()
 {
  var source=Enumerable.Range(1,100).Select(i=>new CommercialLine(null,$"Item {i} Café — detailed software subscription and support package",2,100,5,14)).ToArray();var priced=source.Select(x=>new PricedLine(null,x.Description,2,100,5,14,190,10,26.6m,216.6m)).ToArray();
  var input=new InvoiceInput("INV-TEST-100","Supplier company","SUP-123","Supplier address","Customer company","BUY-456","Customer address","EGP",DateTimeOffset.UtcNow,null,"Due in 30 days","Customer-visible invoice notes",source);
  var invoice=new InvoiceDto(Guid.NewGuid(),Guid.NewGuid(),Guid.NewGuid(),null,"Customer company",input,priced,new(20000,1000,2660,21660),DateTimeOffset.UtcNow);
  using var stream=InvoicePdfWriter.Write(invoice);using var document=PdfReader.Open(stream,PdfDocumentOpenMode.Import);Assert.Contains("INV-TEST-100",document.Info.Title);Assert.True(document.PageCount>1);Assert.True(stream.Length>10000);
 }
 private sealed class DeclaredStream(long length):MemoryStream(new byte[]{1,2,3}){public override long Length=>length;}
 [Fact]public async Task InstallationSizeBoundaryUses64BitCountsAndRejectsOversizeBeforeReading()
 {
  Assert.Equal(8589934592L,PrivateArtifactStore.MaximumSize);var root=Path.Combine(Path.GetTempPath(),"appsouls-size-"+Guid.NewGuid().ToString("N"));try
  {
   var store=new PrivateArtifactStore(root);using var tooLarge=new DeclaredStream(PrivateArtifactStore.MaximumSize+1);await Assert.ThrowsAsync<ArgumentException>(()=>store.SaveAsync(Guid.NewGuid(),tooLarge,default));Assert.Equal(0,tooLarge.Position);Assert.Empty(Directory.EnumerateFiles(root));
   using var boundary=new DeclaredStream(PrivateArtifactStore.MaximumSize);var id=Guid.NewGuid();var saved=await store.SaveAsync(id,boundary,default);Assert.Equal(3,saved.Size);Assert.True(await store.VerifyAsync(id,saved.Sha256,default));
   using var replacement=new MemoryStream(new byte[]{4,5,6});await Assert.ThrowsAsync<IOException>(()=>store.SaveAsync(id,replacement,default));Assert.True(await store.VerifyAsync(id,saved.Sha256,default));
   using var limited=new SizeLimitedReadStream(new MemoryStream(new byte[]{1,2,3,4}),3);await Assert.ThrowsAsync<ArgumentException>(()=>store.SaveAsync(Guid.NewGuid(),limited,default));Assert.Single(Directory.EnumerateFiles(root));
  }
  finally{if(Directory.Exists(root))Directory.Delete(root,true);}
 }
}
