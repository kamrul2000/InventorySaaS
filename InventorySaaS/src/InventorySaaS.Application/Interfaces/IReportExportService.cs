namespace InventorySaaS.Application.Interfaces;

public interface IReportExportService
{
    Task<byte[]> ExportToCsvAsync<T>(IEnumerable<T> data, string[] columns);
    Task<byte[]> ExportToExcelAsync<T>(IEnumerable<T> data, string[] columns);
}
