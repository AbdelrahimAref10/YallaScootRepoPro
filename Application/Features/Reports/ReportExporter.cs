using ClosedXML.Excel;
using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;

namespace Application.Features.Reports
{
    public enum ReportExportFormat
    {
        Excel = 1,
        Pdf = 2
    }

    public class ReportFile
    {
        public byte[] Content { get; set; } = Array.Empty<byte>();
        public string ContentType { get; set; } = string.Empty;
        public string FileName { get; set; } = string.Empty;
    }

    /// <summary>Writes any <see cref="ReportResultDto"/> to Excel or PDF (English labels).</summary>
    public static class ReportExporter
    {
        static ReportExporter()
        {
            QuestPDF.Settings.License = LicenseType.Community;
        }

        public static ReportFile Export(ReportResultDto report, ReportExportFormat format, ReportFilter filter)
        {
            var name = report.Title.Replace(" ", "");
            var period = $"{filter.FromDate?.ToString("yyyy-MM-dd") ?? "…"} → {filter.ToDate?.ToString("yyyy-MM-dd") ?? "…"}";
            return format == ReportExportFormat.Pdf ? ToPdf(report, name, period) : ToExcel(report, name, period);
        }

        private static object? Cell(ReportResultDto report, Dictionary<string, object?> row, ReportColumnDto column) =>
            row.TryGetValue(column.Key, out var value) ? value : null;

        private static string Format(object? value, ReportColumnType type) => value switch
        {
            null => "",
            DateTime d when type == ReportColumnType.Date => d.ToString("yyyy-MM-dd"),
            DateTime d => d.ToString("yyyy-MM-dd HH:mm"),
            decimal m => m.ToString("#,##0.00"),
            _ => value.ToString() ?? ""
        };

        private static ReportFile ToExcel(ReportResultDto report, string name, string period)
        {
            using var workbook = new XLWorkbook();
            var sheet = workbook.Worksheets.Add(name.Length > 31 ? name[..31] : name);
            sheet.Cell(1, 1).Value = $"YallaScoot — {report.Title}";
            sheet.Cell(1, 1).Style.Font.Bold = true;
            sheet.Cell(2, 1).Value = $"Period: {period}";

            var kpiRow = 3;
            foreach (var kpi in report.Kpis)
            {
                sheet.Cell(kpiRow, 1).Value = kpi.Label;
                sheet.Cell(kpiRow, 2).Value = kpi.Value;
                sheet.Cell(kpiRow, 2).Style.NumberFormat.Format = kpi.Type == ReportColumnType.Money ? "#,##0.00" : "0";
                kpiRow++;
            }

            var headerRow = kpiRow + 1;
            for (var c = 0; c < report.Columns.Count; c++)
                sheet.Cell(headerRow, c + 1).Value = report.Columns[c].Label;
            var header = sheet.Range(headerRow, 1, headerRow, report.Columns.Count);
            header.Style.Font.Bold = true;
            header.Style.Fill.BackgroundColor = XLColor.FromHtml("#1F2937");
            header.Style.Font.FontColor = XLColor.White;

            var r = headerRow + 1;
            foreach (var row in report.Rows)
            {
                for (var c = 0; c < report.Columns.Count; c++)
                {
                    var column = report.Columns[c];
                    var cell = sheet.Cell(r, c + 1);
                    var value = Cell(report, row, column);
                    switch (value)
                    {
                        case decimal d:
                            cell.Value = d;
                            cell.Style.NumberFormat.Format = "#,##0.00";
                            break;
                        case int i:
                            cell.Value = i;
                            break;
                        case DateTime dt:
                            cell.Value = dt;
                            cell.Style.DateFormat.Format = column.Type == ReportColumnType.Date ? "yyyy-mm-dd" : "yyyy-mm-dd hh:mm";
                            break;
                        default:
                            cell.Value = Format(value, column.Type);
                            break;
                    }
                }
                r++;
            }

            sheet.Cell(r, 1).Value = "Total";
            sheet.Cell(r, 1).Style.Font.Bold = true;
            for (var c = 0; c < report.Columns.Count; c++)
            {
                if (report.Totals.TryGetValue(report.Columns[c].Key, out var total))
                {
                    sheet.Cell(r, c + 1).Value = total;
                    sheet.Cell(r, c + 1).Style.Font.Bold = true;
                    sheet.Cell(r, c + 1).Style.NumberFormat.Format = report.Columns[c].Type == ReportColumnType.Money ? "#,##0.00" : "0";
                }
            }

            sheet.Columns().AdjustToContents();
            using var stream = new MemoryStream();
            workbook.SaveAs(stream);
            return new ReportFile
            {
                Content = stream.ToArray(),
                ContentType = "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet",
                FileName = $"{name}_{DateTime.UtcNow:yyyyMMdd_HHmm}.xlsx"
            };
        }

        private static ReportFile ToPdf(ReportResultDto report, string name, string period)
        {
            var document = Document.Create(container =>
            {
                container.Page(page =>
                {
                    page.Size(PageSizes.A4.Landscape());
                    page.Margin(20);
                    page.DefaultTextStyle(x => x.FontSize(7));

                    page.Header().Column(col =>
                    {
                        col.Item().Text($"YallaScoot — {report.Title}").SemiBold().FontSize(14);
                        col.Item().Text($"Period: {period}   ·   Generated {DateTime.UtcNow:yyyy-MM-dd HH:mm} UTC").FontColor(Colors.Grey.Darken1);
                        col.Item().PaddingTop(4).Text(string.Join("   ·   ", report.Kpis.Select(k =>
                            $"{k.Label}: {(k.Type == ReportColumnType.Money ? k.Value.ToString("#,##0.00") : k.Value.ToString("0"))}"))).SemiBold();
                        col.Item().PaddingTop(6);
                    });

                    page.Content().Table(table =>
                    {
                        table.ColumnsDefinition(columns =>
                        {
                            foreach (var _ in report.Columns) columns.RelativeColumn();
                        });
                        table.Header(header =>
                        {
                            foreach (var column in report.Columns)
                                header.Cell().Background(Colors.Grey.Darken3).Padding(3).Text(column.Label).FontColor(Colors.White).SemiBold();
                        });
                        foreach (var row in report.Rows)
                            foreach (var column in report.Columns)
                                table.Cell().BorderBottom(0.5f).BorderColor(Colors.Grey.Lighten2).Padding(3)
                                    .Text(Format(Cell(report, row, column), column.Type));
                        foreach (var column in report.Columns)
                        {
                            var text = report.Totals.TryGetValue(column.Key, out var total)
                                ? (column.Type == ReportColumnType.Money ? total.ToString("#,##0.00") : total.ToString("0"))
                                : column == report.Columns[0] ? "Total" : "";
                            table.Cell().Background(Colors.Grey.Lighten3).Padding(3).Text(text).SemiBold();
                        }
                    });

                    page.Footer().AlignRight().Text(x =>
                    {
                        x.CurrentPageNumber();
                        x.Span(" / ");
                        x.TotalPages();
                    });
                });
            });

            return new ReportFile
            {
                Content = document.GeneratePdf(),
                ContentType = "application/pdf",
                FileName = $"{name}_{DateTime.UtcNow:yyyyMMdd_HHmm}.pdf"
            };
        }
    }
}
