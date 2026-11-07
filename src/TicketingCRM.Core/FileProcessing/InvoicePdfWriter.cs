using System.Globalization;
using MigraDoc.DocumentObjectModel;
using MigraDoc.DocumentObjectModel.Tables;
using MigraDoc.Rendering;
using PdfSharp.Fonts;
using TicketingCRM.Domain.Commerce.DTOs;
namespace TicketingCRM.Core.FileProcessing;

public static class InvoicePdfWriter
{
    private static readonly object FontLock = new();
    private sealed class InvoiceFontResolver : IFontResolver
    {
        public FontResolverInfo ResolveTypeface(string familyName,bool bold,bool italic) => new("AppSouls.NotoSans",bold,italic);
        public byte[] GetFont(string faceName) { using var input = typeof(InvoicePdfWriter).Assembly.GetManifestResourceStream("AppSouls.NotoSans.ttf")!; using var output = new MemoryStream(); input.CopyTo(output); return output.ToArray(); }
    }
    public static Stream Write(InvoiceDto invoice)
    {
        lock(FontLock) { GlobalFontSettings.FontResolver ??= new InvoiceFontResolver(); }
        var data=invoice.Details;var document=new Document();document.Info.Title=$"Invoice {data.Number}";document.Info.Author=data.SupplierName;
        var normal=document.Styles[StyleNames.Normal]!;normal.Font.Name="App Souls Sans";normal.Font.Size=9;normal.ParagraphFormat.SpaceAfter=Unit.FromPoint(5);
        document.Styles[StyleNames.Heading1]!.Font.Name="App Souls Sans";document.Styles[StyleNames.Heading1]!.Font.Size=20;document.Styles[StyleNames.Heading1]!.Font.Color=Color.FromRgb(35,100,206);
        var section=document.AddSection();section.PageSetup.PageFormat=PageFormat.A4;section.PageSetup.TopMargin=Unit.FromCentimeter(1.7);section.PageSetup.BottomMargin=Unit.FromCentimeter(1.7);section.PageSetup.LeftMargin=Unit.FromCentimeter(1.6);section.PageSetup.RightMargin=Unit.FromCentimeter(1.6);
        section.AddParagraph("INVOICE",StyleNames.Heading1);section.AddParagraph(data.Number).Format.Font.Bold=true;
        section.AddParagraph($"Issued: {data.IssueDateUtc:yyyy-MM-dd}    Due: {(data.DueDateUtc is {} due ? due.ToString("yyyy-MM-dd") : "As agreed")}    Currency: {data.Currency}");
        section.AddParagraph($"{data.SupplierName}\n{data.SupplierAddress}\nSupplier tax reference: {data.SupplierTaxId}");
        section.AddParagraph($"BILL TO\n{data.BuyerName}\n{data.BuyerAddress}\nBuyer tax reference: {data.BuyerTaxId}");
        section.AddParagraph($"Customer: {invoice.CustomerName}\n{(invoice.OrderId is {} order ? "Order: " + order : "Contract: " + invoice.ContractId)}");
        var table=section.AddTable();table.Borders.Width=.4;table.Borders.Color=Colors.LightGray;table.Format.Font.Size=8;
        foreach(var width in new[]{6.0,1.3,2.0,1.6,1.3,2.0,2.5})table.AddColumn(Unit.FromCentimeter(width));
        var header=table.AddRow();header.HeadingFormat=true;header.Format.Font.Bold=true;header.Shading.Color=Color.FromRgb(233,242,255);
        var titles=new[]{"Description","Qty","Unit price","Discount %","Tax %","Tax","Total"};for(var i=0;i<titles.Length;i++)header.Cells[i].AddParagraph(titles[i]);
        foreach(var line in invoice.Lines)
        {
            var row=table.AddRow();row.Cells[0].AddParagraph(line.Description);row.Cells[1].AddParagraph(line.Quantity.ToString("0.###",CultureInfo.InvariantCulture));row.Cells[2].AddParagraph(Money(line.UnitPrice));row.Cells[3].AddParagraph(Money(line.DiscountPercent));row.Cells[4].AddParagraph(Money(line.TaxPercent));row.Cells[5].AddParagraph(Money(line.Tax));row.Cells[6].AddParagraph(Money(line.Total));
        }
        section.AddParagraph($"Subtotal: {Money(invoice.Totals.Subtotal)} {data.Currency}\nDiscount: {Money(invoice.Totals.Discount)}\nTax: {Money(invoice.Totals.Tax)}\nTOTAL: {Money(invoice.Totals.Total)} {data.Currency}").Format.Font.Bold=true;
        if(!string.IsNullOrWhiteSpace(data.PaymentTerms))section.AddParagraph("Payment terms\n"+data.PaymentTerms);
        if(!string.IsNullOrWhiteSpace(data.Notes))section.AddParagraph("Notes\n"+data.Notes);
        var footer=section.Footers.Primary.AddParagraph();footer.AddText($"Invoice {data.Number}  ·  ");footer.AddPageField();footer.AddText(" / ");footer.AddNumPagesField();footer.Format.Font.Size=7;footer.Format.Alignment=ParagraphAlignment.Right;
        var renderer=new PdfDocumentRenderer{Document=document};renderer.RenderDocument();var stream=new MemoryStream();try{renderer.PdfDocument.Save(stream,false);stream.Position=0;return stream;}catch{stream.Dispose();throw;}
    }
    private static string Money(decimal value)=>value.ToString("N2",CultureInfo.InvariantCulture);
}
