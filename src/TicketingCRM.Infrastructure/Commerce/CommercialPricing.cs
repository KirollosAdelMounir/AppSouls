using TicketingCRM.Domain.Commerce.DTOs;
namespace TicketingCRM.Infrastructure.Commerce;
public static class CommercialPricing
{
 public static (IReadOnlyList<PricedLine> Lines,CommercialTotals Totals) Calculate(IReadOnlyList<CommercialLine>? source)
 {
  if(source is null || source.Count is < 1 or > 100)throw new ArgumentException("Add between one and 100 invoice/order lines.");
  var lines=new List<PricedLine>();
  foreach(var line in source)
  {
   if(line is null || string.IsNullOrWhiteSpace(line.Description) || line.Description.Length > 500 || line.Quantity <= 0 || line.Quantity > 1000000 || decimal.Round(line.Quantity,3)!=line.Quantity || line.UnitPrice < 0 || line.UnitPrice > 999999999.99m || decimal.Round(line.UnitPrice,2)!=line.UnitPrice || line.DiscountPercent is < 0 or > 100 || line.TaxPercent is < 0 or > 100 || decimal.Round(line.DiscountPercent,2)!=line.DiscountPercent || decimal.Round(line.TaxPercent,2)!=line.TaxPercent)throw new ArgumentException("Check line descriptions, quantities, prices, discounts and tax percentages.");
   var gross=Money(line.Quantity*line.UnitPrice);var discount=Money(gross*line.DiscountPercent/100);var net=gross-discount;var tax=Money(net*line.TaxPercent/100);
   lines.Add(new(line.ProductId,line.Description.Trim(),line.Quantity,line.UnitPrice,line.DiscountPercent,line.TaxPercent,net,discount,tax,net+tax));
  }
  return(lines,new(lines.Sum(x=>x.Net+x.Discount),lines.Sum(x=>x.Discount),lines.Sum(x=>x.Tax),lines.Sum(x=>x.Total)));
 }
 private static decimal Money(decimal value)=>decimal.Round(value,2,MidpointRounding.AwayFromZero);
}
