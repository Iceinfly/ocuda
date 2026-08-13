using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Ocuda.Models;
using Ocuda.Ops.Models;
using Ocuda.Ops.Service.Filters;
using Ocuda.Ops.Service.Interfaces.Ops.Repositories;
using Ocuda.Ops.Service.Interfaces.Ops.Services;
using Ocuda.Ops.Service.Interfaces.Promenade.Repositories;
using Ocuda.Utility.Abstract;
using Ocuda.Utility.Models;

namespace Ocuda.Ops.Service
{
    public class EmediaReportingService(
        IDateTimeProvider dateTimeProvider,
        ILogger<EmediaReportingService> logger,
        IEmediaAccessRepository emediaAccessRepository,
        IEmediaRepository emediaRepository,
        IEmediaStatsRepository emediaStatsRepository)
        : IEmediaReportingService
    {
        public async Task<bool> AnyAsync()
        {
            return await emediaStatsRepository.AnyAsync();
        }

        public async Task<IEnumerable<DisplayReport>> GetReportAsync(ReportCriteria criteria)
        {
            ArgumentNullException.ThrowIfNull(criteria);

            var data = await emediaStatsRepository.GetReportAsync(criteria.StartDate);

            var emediaNames = await emediaRepository.GetIdsNamesAsync();

            return [

                new DisplayReport(
                    $"{criteria.Report.Name} {data.First().Month}/{data.First().Year}",
                    dateTimeProvider.Now)
                {
                    HeaderRow = [
                        "Electronic Resource",
                        "Accesses",
                    ],
                    Data =
                    data.ToDictionary(
                        k => emediaNames.TryGetValue(k.EmediaId, out string value)
                            ? value
                            : "Unknown",
                        v => string.IsNullOrEmpty(criteria.NumberFormat)
                                ? (object)v.Accesses
                                : v.Accesses.ToString(criteria.NumberFormat,
                                    CultureInfo.CurrentCulture))
                        .OrderBy(_ => _.Key)
                        .Select(_ => new List<object>
                        {
                            _.Key,
                            _.Value
                        }),
                    FooterRow = [
                        "Total",
                        string.IsNullOrEmpty(criteria.NumberFormat)
                            ? data.Sum(_ => _.Accesses)
                            : data.Sum(_ => _.Accesses)
                                .ToString(criteria.NumberFormat, CultureInfo.CurrentCulture),
                        ],
                }

            ];
        }

        public async Task<DataWithCount<IDictionary<DateTime, int?>>> GetStatsAsync(
            BaseFilter<string> filter)
        {
            return new DataWithCount<IDictionary<DateTime, int?>>
            {
                Count = await emediaStatsRepository.GetDateCountAsync(filter),
                Data = await emediaStatsRepository.GetDatesAsync(filter),
            };
        }

        public async Task RunPendingReportsAsync()
        {
            var timer = Stopwatch.StartNew();
            var determineReports = await emediaAccessRepository.GetDatesAfterAsync(
                await emediaStatsRepository.GetLatestDateAsync() ?? DateTime.MinValue);

            var now = dateTimeProvider.Now;

            var reportsToRun = determineReports
                .Where(_ => _ < new DateTime(now.Year, now.Month, 1))
                .Order();

            if (reportsToRun?.Count() > 0)
            {
                logger.LogInformation(
                    "Found {ReportsToRunCount} electronic resource access reports prior to {Date} to run",
                    reportsToRun?.Count(),
                    new DateTime(now.Year, now.Month, 1).ToShortDateString());

                foreach (var date in reportsToRun)
                {
                    var startedAt = timer.ElapsedMilliseconds;
                    var resultStats = await emediaAccessRepository.GetStatsAsync(date);

                    foreach (var stats in resultStats)
                    {
                        stats.Year = date.Year;
                        stats.Month = date.Month;
                        stats.CreatedAt = now;
                    }

                    await emediaStatsRepository.SaveStatsAsync(resultStats);
                    logger.LogInformation(
                        "Finished running electronic resource access report for {Month}/{Year} in {ElapsedMs} ms",
                        date.Month,
                        date.Year,
                        timer.ElapsedMilliseconds - startedAt);
                }

                logger.LogInformation(
                    "Finished all {Count} electronic resource access reports in {ElapsedMs} ms",
                    reportsToRun.Count(),
                    timer.ElapsedMilliseconds);
            }
        }
    }
}
