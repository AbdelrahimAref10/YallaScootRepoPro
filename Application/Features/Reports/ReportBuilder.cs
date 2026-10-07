namespace Application.Features.Reports
{
    /// <summary>Declares columns, collects rows and sums the total columns.</summary>
    public sealed class ReportBuilder
    {
        private readonly ReportResultDto _result;

        public ReportBuilder(string key, string title)
        {
            _result = new ReportResultDto { Key = key, Title = title };
        }

        public ReportBuilder Column(string key, string label, ReportColumnType type, bool total = false, string? valueKeyPrefix = null)
        {
            _result.Columns.Add(new ReportColumnDto
            {
                Key = key,
                Label = label,
                LabelKey = $"reports.col.{key}",
                Type = type,
                Total = total,
                ValueKeyPrefix = valueKeyPrefix
            });
            return this;
        }

        public ReportBuilder Text(string key, string label) => Column(key, label, ReportColumnType.Text);
        public ReportBuilder Number(string key, string label, bool total = true) => Column(key, label, ReportColumnType.Number, total);
        public ReportBuilder Money(string key, string label, bool total = true) => Column(key, label, ReportColumnType.Money, total);
        public ReportBuilder Date(string key, string label) => Column(key, label, ReportColumnType.Date);
        public ReportBuilder DateTime(string key, string label) => Column(key, label, ReportColumnType.DateTime);
        public ReportBuilder Badge(string key, string label, string valueKeyPrefix = "reports.value.") =>
            Column(key, label, ReportColumnType.Badge, valueKeyPrefix: valueKeyPrefix);

        public ReportBuilder Row(Dictionary<string, object?> row)
        {
            _result.Rows.Add(row);
            return this;
        }

        public ReportBuilder Kpi(string key, string label, decimal value, ReportColumnType type = ReportColumnType.Money)
        {
            _result.Kpis.Add(new ReportKpiDto { Label = label, LabelKey = $"reports.kpi.{key}", Value = value, Type = type });
            return this;
        }

        /// <summary>Sum of a total column over the rows added so far.</summary>
        public decimal Sum(string key) => _result.Rows.Sum(r => r.TryGetValue(key, out var v) && v != null ? Convert.ToDecimal(v) : 0m);

        public ReportResultDto Build()
        {
            foreach (var column in _result.Columns.Where(c => c.Total))
                _result.Totals[column.Key] = Sum(column.Key);
            return _result;
        }
    }
}
