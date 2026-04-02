using InventorySaaS.Application.Features.Reports.DTOs;
using InventorySaaS.Application.Interfaces;
using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;

namespace InventorySaaS.Infrastructure.Services.Reports;

public class PdfReportService : IPdfReportService
{
    // Brand colors
    private static readonly string PrimaryColor = "#1565C0";    // Blue
    private static readonly string PrimaryDark = "#0D47A1";
    private static readonly string AccentColor = "#FF6F00";     // Amber
    private static readonly string DangerColor = "#C62828";     // Red
    private static readonly string SuccessColor = "#2E7D32";    // Green
    private static readonly string HeaderBg = "#1565C0";
    private static readonly string HeaderText = "#FFFFFF";
    private static readonly string StripeColor = "#F5F7FA";
    private static readonly string BorderColor = "#E0E0E0";
    private static readonly string TextPrimary = "#212121";
    private static readonly string TextSecondary = "#757575";

    public byte[] GenerateStockSummaryPdf(List<StockSummaryReportDto> data, string tenantName)
    {
        var totalQty = data.Sum(x => x.QuantityOnHand);
        var totalValue = data.Sum(x => x.TotalValue);

        return GeneratePdf("Stock Summary Report", tenantName, container =>
        {
            container.Column(col =>
            {
                // Summary Cards
                col.Item().PaddingBottom(15).Row(row =>
                {
                    SummaryCard(row, "Total Products", data.Count.ToString(), PrimaryColor);
                    row.ConstantItem(12);
                    SummaryCard(row, "Total Quantity", totalQty.ToString("N0"), AccentColor);
                    row.ConstantItem(12);
                    SummaryCard(row, "Total Inventory Value", totalValue.ToString("C2"), SuccessColor);
                });

                // Table
                col.Item().Table(table =>
                {
                    table.ColumnsDefinition(columns =>
                    {
                        columns.RelativeColumn(2.5f); // Product
                        columns.RelativeColumn(1.5f); // SKU
                        columns.RelativeColumn(1.5f); // Category
                        columns.RelativeColumn(1.5f); // Warehouse
                        columns.RelativeColumn(1);    // Qty
                        columns.RelativeColumn(1.2f); // Unit Cost
                        columns.RelativeColumn(1.3f); // Total Value
                    });

                    HeaderRow(table, new HashSet<int>{4, 5, 6}, "Product", "SKU", "Category", "Warehouse", "Qty", "Unit Cost", "Total Value");

                    for (int i = 0; i < data.Count; i++)
                    {
                        var item = data[i];
                        var bg = i % 2 == 1 ? StripeColor : "#FFFFFF";
                        DataCell(table, item.ProductName, bg);
                        DataCell(table, item.Sku, bg, fontColor: TextSecondary);
                        DataCell(table, item.CategoryName, bg);
                        DataCell(table, item.WarehouseName, bg);
                        DataCell(table, item.QuantityOnHand.ToString("N0"), bg, alignRight: true);
                        DataCell(table, item.UnitCost.ToString("C2"), bg, alignRight: true);
                        DataCell(table, item.TotalValue.ToString("C2"), bg, alignRight: true, bold: true);
                    }

                    TotalRow(table, 7, new[] {
                        (4, "TOTAL"),
                        (5, totalQty.ToString("N0")),
                        (6, ""),
                        (7, totalValue.ToString("C2"))
                    });
                });
            });
        });
    }

    public byte[] GenerateLowStockPdf(List<LowStockReportDto> data, string tenantName)
    {
        return GeneratePdf("Low Stock Alert Report", tenantName, container =>
        {
            container.Column(col =>
            {
                // Summary
                col.Item().PaddingBottom(15).Row(row =>
                {
                    SummaryCard(row, "Items Below Reorder Level", data.Count.ToString(), DangerColor);
                    row.ConstantItem(12);
                    SummaryCard(row, "Total Deficit", data.Sum(x => x.Deficit).ToString("N0"), AccentColor);
                    row.ConstantItem(12);
                    SummaryCard(row, "Most Critical", data.Count > 0 ? data.OrderByDescending(x => x.Deficit).First().ProductName : "N/A", PrimaryColor);
                });

                col.Item().Table(table =>
                {
                    table.ColumnsDefinition(columns =>
                    {
                        columns.RelativeColumn(2.5f); // Product
                        columns.RelativeColumn(1.5f); // SKU
                        columns.RelativeColumn(2);    // Warehouse
                        columns.RelativeColumn(1.2f); // Current
                        columns.RelativeColumn(1.2f); // Reorder Level
                        columns.RelativeColumn(1.2f); // Deficit
                    });

                    HeaderRow(table, new HashSet<int>{3, 4, 5}, "Product", "SKU", "Warehouse", "Current Stock", "Reorder Level", "Deficit");

                    for (int i = 0; i < data.Count; i++)
                    {
                        var item = data[i];
                        var bg = i % 2 == 1 ? StripeColor : "#FFFFFF";
                        DataCell(table, item.ProductName, bg);
                        DataCell(table, item.Sku, bg, fontColor: TextSecondary);
                        DataCell(table, item.WarehouseName, bg);
                        DataCell(table, item.CurrentStock.ToString("N0"), bg, alignRight: true, fontColor: AccentColor, bold: true);
                        DataCell(table, item.ReorderLevel.ToString("N0"), bg, alignRight: true);
                        DataCell(table, item.Deficit.ToString("N0"), bg, alignRight: true, fontColor: DangerColor, bold: true);
                    }
                });
            });
        });
    }

    public byte[] GenerateExpiryPdf(List<ExpiryReportDto> data, string tenantName)
    {
        var critical = data.Count(x => x.DaysUntilExpiry <= 7);
        var warning = data.Count(x => x.DaysUntilExpiry > 7 && x.DaysUntilExpiry <= 30);

        return GeneratePdf("Expiry Alert Report", tenantName, container =>
        {
            container.Column(col =>
            {
                col.Item().PaddingBottom(15).Row(row =>
                {
                    SummaryCard(row, "Total Expiring Items", data.Count.ToString(), AccentColor);
                    row.ConstantItem(12);
                    SummaryCard(row, "Critical (< 7 days)", critical.ToString(), DangerColor);
                    row.ConstantItem(12);
                    SummaryCard(row, "Warning (< 30 days)", warning.ToString(), AccentColor);
                });

                col.Item().Table(table =>
                {
                    table.ColumnsDefinition(columns =>
                    {
                        columns.RelativeColumn(2.5f); // Product
                        columns.RelativeColumn(1.5f); // SKU
                        columns.RelativeColumn(1.5f); // Warehouse
                        columns.RelativeColumn(1.2f); // Batch
                        columns.RelativeColumn(1.5f); // Expiry Date
                        columns.RelativeColumn(1);    // Qty
                        columns.RelativeColumn(1.2f); // Days Left
                    });

                    HeaderRow(table, new HashSet<int>{5, 6}, "Product", "SKU", "Warehouse", "Batch", "Expiry Date", "Qty", "Days Left");

                    for (int i = 0; i < data.Count; i++)
                    {
                        var item = data[i];
                        var bg = i % 2 == 1 ? StripeColor : "#FFFFFF";
                        var daysColor = item.DaysUntilExpiry <= 7 ? DangerColor : item.DaysUntilExpiry <= 30 ? AccentColor : TextPrimary;

                        DataCell(table, item.ProductName, bg);
                        DataCell(table, item.Sku, bg, fontColor: TextSecondary);
                        DataCell(table, item.WarehouseName, bg);
                        DataCell(table, item.BatchNumber ?? "-", bg);
                        DataCell(table, item.ExpiryDate.ToString("MMM dd, yyyy"), bg, fontColor: daysColor);
                        DataCell(table, item.Quantity.ToString("N0"), bg, alignRight: true);
                        DaysLeftCell(table, item.DaysUntilExpiry, bg);
                    }
                });
            });
        });
    }

    public byte[] GenerateInventoryValuationPdf(List<InventoryValuationDto> data, string tenantName)
    {
        var totalProducts = data.Sum(x => x.ProductCount);
        var totalCost = data.Sum(x => x.TotalCostValue);
        var totalSelling = data.Sum(x => x.TotalSellingValue);
        var profitMargin = totalSelling > 0 ? ((totalSelling - totalCost) / totalSelling * 100) : 0;

        return GeneratePdf("Inventory Valuation Report", tenantName, container =>
        {
            container.Column(col =>
            {
                col.Item().PaddingBottom(15).Row(row =>
                {
                    SummaryCard(row, "Total Cost Value", totalCost.ToString("C2"), PrimaryColor);
                    row.ConstantItem(12);
                    SummaryCard(row, "Total Selling Value", totalSelling.ToString("C2"), SuccessColor);
                    row.ConstantItem(12);
                    SummaryCard(row, "Profit Margin", profitMargin.ToString("F1") + "%", AccentColor);
                });

                col.Item().Table(table =>
                {
                    table.ColumnsDefinition(columns =>
                    {
                        columns.RelativeColumn(3);    // Category
                        columns.RelativeColumn(1.5f); // Products
                        columns.RelativeColumn(2);    // Cost Value
                        columns.RelativeColumn(2);    // Selling Value
                        columns.RelativeColumn(1.5f); // Margin
                    });

                    HeaderRow(table, new HashSet<int>{1, 2, 3, 4}, "Category", "Products", "Cost Value", "Selling Value", "Margin");

                    for (int i = 0; i < data.Count; i++)
                    {
                        var item = data[i];
                        var bg = i % 2 == 1 ? StripeColor : "#FFFFFF";
                        var margin = item.TotalSellingValue > 0
                            ? ((item.TotalSellingValue - item.TotalCostValue) / item.TotalSellingValue * 100)
                            : 0;

                        DataCell(table, item.CategoryName, bg, bold: true);
                        DataCell(table, item.ProductCount.ToString(), bg, alignRight: true);
                        DataCell(table, item.TotalCostValue.ToString("C2"), bg, alignRight: true);
                        DataCell(table, item.TotalSellingValue.ToString("C2"), bg, alignRight: true, bold: true);
                        DataCell(table, margin.ToString("F1") + "%", bg, alignRight: true,
                            fontColor: margin >= 30 ? SuccessColor : margin >= 15 ? AccentColor : DangerColor);
                    }

                    TotalRow(table, 5, new[] {
                        (1, "TOTAL"),
                        (2, totalProducts.ToString()),
                        (3, totalCost.ToString("C2")),
                        (4, totalSelling.ToString("C2")),
                        (5, profitMargin.ToString("F1") + "%")
                    });
                });
            });
        });
    }

    // ─── Layout Helpers ────────────────────────────────────────

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
                page.DefaultTextStyle(x => x.FontSize(9).FontColor(TextPrimary));

                // ── Header ──
                page.Header().Column(col =>
                {
                    col.Item().Row(row =>
                    {
                        row.RelativeItem().Column(inner =>
                        {
                            inner.Item().Text("InventorySaaS").FontSize(18).Bold().FontColor(PrimaryColor);
                            inner.Item().Text(tenantName).FontSize(10).FontColor(TextSecondary);
                        });
                        row.ConstantItem(200).AlignRight().AlignMiddle().Column(inner =>
                        {
                            inner.Item().AlignRight().Text(DateTime.UtcNow.ToString("MMMM dd, yyyy")).FontSize(9).FontColor(TextSecondary);
                            inner.Item().AlignRight().Text(DateTime.UtcNow.ToString("HH:mm") + " UTC").FontSize(8).FontColor(TextSecondary);
                        });
                    });
                    col.Item().PaddingTop(8).Text(title).FontSize(20).Bold().FontColor(PrimaryDark);
                    col.Item().PaddingTop(6).PaddingBottom(12).LineHorizontal(2).LineColor(PrimaryColor);
                });

                // ── Content ──
                page.Content().Element(content);

                // ── Footer ──
                page.Footer().Column(col =>
                {
                    col.Item().LineHorizontal(0.5f).LineColor(BorderColor);
                    col.Item().PaddingTop(6).Row(row =>
                    {
                        row.RelativeItem().Text(text =>
                        {
                            text.Span("Generated by ").FontSize(7).FontColor(TextSecondary);
                            text.Span("InventorySaaS").FontSize(7).Bold().FontColor(PrimaryColor);
                            text.Span(" | " + DateTime.UtcNow.ToString("yyyy-MM-dd HH:mm:ss") + " UTC").FontSize(7).FontColor(TextSecondary);
                        });
                        row.ConstantItem(100).AlignRight().Text(text =>
                        {
                            text.Span("Page ").FontSize(7).FontColor(TextSecondary);
                            text.CurrentPageNumber().FontSize(7).Bold().FontColor(TextPrimary);
                            text.Span(" of ").FontSize(7).FontColor(TextSecondary);
                            text.TotalPages().FontSize(7).Bold().FontColor(TextPrimary);
                        });
                    });
                });
            });
        });

        return document.GeneratePdf();
    }

    private static void SummaryCard(RowDescriptor row, string label, string value, string color)
    {
        row.RelativeItem().Border(1).BorderColor(BorderColor).Background("#FFFFFF")
            .Padding(10).Column(col =>
            {
                col.Item().Text(label).FontSize(8).FontColor(TextSecondary);
                col.Item().PaddingTop(4).Text(value).FontSize(16).Bold().FontColor(color);
            });
    }

    private static void HeaderRow(TableDescriptor table, params string[] headers)
    {
        HeaderRow(table, null, headers);
    }

    private static void HeaderRow(TableDescriptor table, HashSet<int>? rightAlignIndices, params string[] headers)
    {
        for (int i = 0; i < headers.Length; i++)
        {
            var cell = table.Cell()
                .Background(HeaderBg)
                .Padding(8);

            if (rightAlignIndices != null && rightAlignIndices.Contains(i))
                cell = cell.AlignRight();

            cell.Text(headers[i])
                .FontSize(8.5f)
                .Bold()
                .FontColor(HeaderText);
        }
    }

    private static void DataCell(TableDescriptor table, string text, string bgColor,
        bool alignRight = false, bool bold = false, string? fontColor = null)
    {
        var cell = table.Cell()
            .Background(bgColor)
            .BorderBottom(0.5f)
            .BorderColor(BorderColor)
            .PaddingHorizontal(8)
            .PaddingVertical(6);

        if (alignRight) cell = cell.AlignRight();

        var txt = cell.Text(text).FontSize(8.5f);
        if (bold) txt.Bold();
        if (fontColor != null) txt.FontColor(fontColor);
    }

    private static void DaysLeftCell(TableDescriptor table, int days, string bgColor)
    {
        var pillColor = days <= 7 ? DangerColor : days <= 30 ? AccentColor : SuccessColor;
        var pillBg = days <= 7 ? "#FDECEA" : days <= 30 ? "#FFF3E0" : "#E8F5E9";

        table.Cell()
            .Background(bgColor)
            .BorderBottom(0.5f)
            .BorderColor(BorderColor)
            .PaddingHorizontal(8)
            .PaddingVertical(4)
            .AlignRight()
            .AlignMiddle()
            .Element(el =>
            {
                el.Background(pillBg)
                  .Padding(3)
                  .PaddingHorizontal(8)
                  .AlignCenter()
                  .Text(days.ToString() + " days")
                  .FontSize(8)
                  .Bold()
                  .FontColor(pillColor);
            });
    }

    private static void TotalRow(TableDescriptor table, int totalColumns, (int colIndex, string text)[] values)
    {
        for (int col = 1; col <= totalColumns; col++)
        {
            var match = values.FirstOrDefault(v => v.colIndex == col);
            var cell = table.Cell()
                .Background("#E8EAF6")
                .BorderTop(2)
                .BorderColor(PrimaryColor)
                .PaddingHorizontal(8)
                .PaddingVertical(8);

            if (match.text != null)
            {
                if (col > 1) cell = cell.AlignRight();
                cell.Text(match.text).FontSize(9).Bold().FontColor(PrimaryDark);
            }
            else
            {
                cell.Text(""); // empty cell
            }
        }
    }
}
