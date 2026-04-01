using InventorySaaS.Application.Features.Reports.DTOs;
using InventorySaaS.Application.Interfaces;
using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;

namespace InventorySaaS.Infrastructure.Services.Reports;

public class PdfReportService : IPdfReportService
{
    public byte[] GenerateStockSummaryPdf(List<StockSummaryReportDto> data, string tenantName)
    {
        return GeneratePdf("Stock Summary Report", tenantName, container =>
        {
            container.Table(table =>
            {
                table.ColumnsDefinition(columns =>
                {
                    columns.RelativeColumn(3);  // Product
                    columns.RelativeColumn(2);  // SKU
                    columns.RelativeColumn(2);  // Category
                    columns.RelativeColumn(2);  // Warehouse
                    columns.RelativeColumn(1);  // Qty
                    columns.RelativeColumn(1.5f); // Unit Cost
                    columns.RelativeColumn(1.5f); // Total Value
                });

                // Header
                TableHeader(table, "Product", "SKU", "Category", "Warehouse", "Qty", "Unit Cost", "Total Value");

                foreach (var item in data)
                {
                    TableCell(table, item.ProductName);
                    TableCell(table, item.Sku);
                    TableCell(table, item.CategoryName);
                    TableCell(table, item.WarehouseName);
                    TableCell(table, item.QuantityOnHand.ToString(), true);
                    TableCell(table, item.UnitCost.ToString("C2"), true);
                    TableCell(table, item.TotalValue.ToString("C2"), true);
                }

                // Totals row
                table.Cell().ColumnSpan(4).PaddingVertical(4).Text("TOTAL").Bold().FontSize(9);
                TableCell(table, data.Sum(x => x.QuantityOnHand).ToString(), true, true);
                TableCell(table, "", true);
                TableCell(table, data.Sum(x => x.TotalValue).ToString("C2"), true, true);
            });
        });
    }

    public byte[] GenerateLowStockPdf(List<LowStockReportDto> data, string tenantName)
    {
        return GeneratePdf("Low Stock Report", tenantName, container =>
        {
            container.Table(table =>
            {
                table.ColumnsDefinition(columns =>
                {
                    columns.RelativeColumn(3);  // Product
                    columns.RelativeColumn(2);  // SKU
                    columns.RelativeColumn(2);  // Warehouse
                    columns.RelativeColumn(1.5f); // Current Stock
                    columns.RelativeColumn(1.5f); // Reorder Level
                    columns.RelativeColumn(1.5f); // Deficit
                });

                TableHeader(table, "Product", "SKU", "Warehouse", "Current", "Reorder Lvl", "Deficit");

                foreach (var item in data)
                {
                    TableCell(table, item.ProductName);
                    TableCell(table, item.Sku);
                    TableCell(table, item.WarehouseName);
                    TableCell(table, item.CurrentStock.ToString(), true);
                    TableCell(table, item.ReorderLevel.ToString(), true);
                    TableCell(table, item.Deficit.ToString(), true, true);
                }
            });
        });
    }

    public byte[] GenerateExpiryPdf(List<ExpiryReportDto> data, string tenantName)
    {
        return GeneratePdf("Expiry Alert Report", tenantName, container =>
        {
            container.Table(table =>
            {
                table.ColumnsDefinition(columns =>
                {
                    columns.RelativeColumn(3);  // Product
                    columns.RelativeColumn(2);  // SKU
                    columns.RelativeColumn(2);  // Warehouse
                    columns.RelativeColumn(1.5f); // Batch
                    columns.RelativeColumn(2);  // Expiry Date
                    columns.RelativeColumn(1);  // Qty
                    columns.RelativeColumn(1.5f); // Days Left
                });

                TableHeader(table, "Product", "SKU", "Warehouse", "Batch", "Expiry Date", "Qty", "Days Left");

                foreach (var item in data)
                {
                    TableCell(table, item.ProductName);
                    TableCell(table, item.Sku);
                    TableCell(table, item.WarehouseName);
                    TableCell(table, item.BatchNumber ?? "-");
                    TableCell(table, item.ExpiryDate.ToString("yyyy-MM-dd"));
                    TableCell(table, item.Quantity.ToString(), true);
                    TableCell(table, item.DaysUntilExpiry.ToString(), true, item.DaysUntilExpiry <= 7);
                }
            });
        });
    }

    public byte[] GenerateInventoryValuationPdf(List<InventoryValuationDto> data, string tenantName)
    {
        return GeneratePdf("Inventory Valuation Report", tenantName, container =>
        {
            container.Table(table =>
            {
                table.ColumnsDefinition(columns =>
                {
                    columns.RelativeColumn(3);   // Category
                    columns.RelativeColumn(1.5f); // Products
                    columns.RelativeColumn(2);   // Cost Value
                    columns.RelativeColumn(2);   // Selling Value
                });

                TableHeader(table, "Category", "Products", "Cost Value", "Selling Value");

                foreach (var item in data)
                {
                    TableCell(table, item.CategoryName);
                    TableCell(table, item.ProductCount.ToString(), true);
                    TableCell(table, item.TotalCostValue.ToString("C2"), true);
                    TableCell(table, item.TotalSellingValue.ToString("C2"), true);
                }

                // Totals row
                table.Cell().PaddingVertical(4).Text("TOTAL").Bold().FontSize(9);
                TableCell(table, data.Sum(x => x.ProductCount).ToString(), true, true);
                TableCell(table, data.Sum(x => x.TotalCostValue).ToString("C2"), true, true);
                TableCell(table, data.Sum(x => x.TotalSellingValue).ToString("C2"), true, true);
            });
        });
    }

    private static byte[] GeneratePdf(string title, string tenantName, Action<IContainer> content)
    {
        QuestPDF.Settings.License = LicenseType.Community;

        var document = Document.Create(doc =>
        {
            doc.Page(page =>
            {
                page.Size(PageSizes.A4.Landscape());
                page.MarginHorizontal(30);
                page.MarginVertical(25);
                page.DefaultTextStyle(x => x.FontSize(9));

                page.Header().Column(col =>
                {
                    col.Item().Row(row =>
                    {
                        row.RelativeItem().Text(tenantName).FontSize(14).Bold().FontColor(Colors.Blue.Darken2);
                        row.RelativeItem().AlignRight().Text(DateTime.UtcNow.ToString("yyyy-MM-dd HH:mm UTC")).FontSize(8).FontColor(Colors.Grey.Darken1);
                    });
                    col.Item().PaddingTop(4).Text(title).FontSize(16).Bold();
                    col.Item().PaddingTop(8).LineHorizontal(1).LineColor(Colors.Grey.Lighten1);
                    col.Item().PaddingBottom(10);
                });

                page.Content().Element(content);

                page.Footer().AlignCenter().Text(text =>
                {
                    text.Span("Page ").FontSize(8);
                    text.CurrentPageNumber().FontSize(8);
                    text.Span(" of ").FontSize(8);
                    text.TotalPages().FontSize(8);
                    text.Span("  |  Generated by InventorySaaS").FontSize(8).FontColor(Colors.Grey.Darken1);
                });
            });
        });

        return document.GeneratePdf();
    }

    private static void TableHeader(TableDescriptor table, params string[] headers)
    {
        foreach (var header in headers)
        {
            table.Cell().BorderBottom(1).BorderColor(Colors.Black)
                .Padding(4).Text(header).Bold().FontSize(9);
        }
    }

    private static void TableCell(TableDescriptor table, string text, bool alignRight = false, bool bold = false)
    {
        var cell = table.Cell().BorderBottom(0.5f).BorderColor(Colors.Grey.Lighten2).Padding(3);
        var textDescriptor = cell.Text(text).FontSize(8);
        if (alignRight) cell.AlignRight();
        if (bold) textDescriptor.Bold();
    }
}
