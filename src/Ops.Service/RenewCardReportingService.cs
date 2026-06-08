using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Ocuda.Models;
using Ocuda.Ops.Models;
using Ocuda.Ops.Models.Keys.SiteSetting;
using Ocuda.Ops.Service.Filters;
using Ocuda.Ops.Service.Interfaces.Ops.Repositories;
using Ocuda.Ops.Service.Interfaces.Ops.Services;
using Ocuda.Utility.Abstract;
using Ocuda.Utility.Models;

namespace Ocuda.Ops.Service
{
    public class RenewCardReportingService(
        IDateTimeProvider dateTimeProvider,
        ILogger<RenewCardReportingService> logger,
        IRenewCardResponseRepository renewCardResponseRepository,
        IRenewCardResultRepository renewCardResultRepository,
        IRenewCardStatsRepository renewCardStatsRepository,
        ISiteSettingService siteSettingService)
        : IRenewCardReportingService
    {
        public async Task<bool> AnyAsync()
        {
            return await renewCardStatsRepository.AnyAsync();
        }

        public async Task<IEnumerable<DisplayReport>> GetReportAsync(ReportCriteria criteria)
        {
            ArgumentNullException.ThrowIfNull(criteria);

            var data = await renewCardStatsRepository.GetReportAsync(criteria.StartDate.Year);

            return [

                new DisplayReport(
                $"{criteria.Report.Name} {data.First().Year}",
                dateTimeProvider.Now)
                {
                    HeaderRow = ["Month",
                        "Accepted",
                        "Denied",
                        "Discarded",
                        "Partial",
                        "Unprocessed",
                        "Stats collected",
                    ],
                    Data = data.Select(_ => new List<object>
                    {
                        $"{_.Month}/{_.Year}",
                        string.IsNullOrEmpty(criteria.NumberFormat)
                            ? _.Accepted
                            : _.Accepted.ToString(criteria.NumberFormat, CultureInfo.CurrentCulture),
                        string.IsNullOrEmpty(criteria.NumberFormat)
                            ? _.Denied
                            : _.Denied.ToString(criteria.NumberFormat, CultureInfo.CurrentCulture),
                        string.IsNullOrEmpty(criteria.NumberFormat)
                            ? _.Discarded
                            : _.Discarded.ToString(criteria.NumberFormat, CultureInfo.CurrentCulture),
                        string.IsNullOrEmpty(criteria.NumberFormat)
                            ? _.Partial
                            : _.Partial.ToString(criteria.NumberFormat, CultureInfo.CurrentCulture),
                        string.IsNullOrEmpty(criteria.NumberFormat)
                            ? _.Unprocessed
                            : _.Unprocessed.ToString(criteria.NumberFormat, CultureInfo.CurrentCulture),
                        _.CreatedAt.ToString(CultureInfo.CurrentCulture),
                    }),
                    FooterRow = [
                        "Total",
                        string.IsNullOrEmpty(criteria.NumberFormat)
                            ? data.Sum(_ => _.Accepted)
                            : data.Sum(_ => _.Accepted)
                                .ToString(criteria.NumberFormat, CultureInfo.CurrentCulture),
                        string.IsNullOrEmpty(criteria.NumberFormat)
                            ? data.Sum(_ => _.Denied)
                            : data.Sum(_ => _.Denied)
                                .ToString(criteria.NumberFormat, CultureInfo.CurrentCulture),
                        string.IsNullOrEmpty(criteria.NumberFormat)
                            ? data.Sum(_ => _.Discarded)
                            : data.Sum(_ => _.Discarded)
                                .ToString(criteria.NumberFormat, CultureInfo.CurrentCulture),
                        string.IsNullOrEmpty(criteria.NumberFormat)
                            ? data.Sum(_ => _.Partial)
                            : data.Sum(_ => _.Partial)
                                .ToString(criteria.NumberFormat, CultureInfo.CurrentCulture),
                        string.IsNullOrEmpty(criteria.NumberFormat)
                            ? data.Sum(_ => _.Unprocessed)
                            : data.Sum(_ => _.Unprocessed)
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
                Count = await renewCardStatsRepository.GetDateCountAsync(filter),
                Data = await renewCardStatsRepository.GetDatesAsync(filter),
            };
        }

        public async Task RunPendingReportsAsync()
        {
            var timer = Stopwatch.StartNew();
            var determineReports = await renewCardResultRepository.GetDatesAfterAsync(
                await renewCardStatsRepository.GetLatestDateAsync() ?? DateTime.MinValue);

            var now = dateTimeProvider.Now;

            var lookbackDays = await siteSettingService.GetSettingIntAsync(RenewCard.StatsLookback);

            var runReportsPriorTo = now.AddDays(Math.Abs(lookbackDays) * -1);

            var reportsToRun = determineReports.Where(_ => _ <= runReportsPriorTo).Order();

            if (reportsToRun?.Count() > 0)
            {
                logger.LogInformation(
                    "Found {ReportsToRunCount} renew card stats reports prior to {Date} ({Lookback} lookback) to run",
                    reportsToRun?.Count(),
                    runReportsPriorTo.ToShortDateString(),
                    Math.Abs(lookbackDays) * -1);

                foreach (var date in reportsToRun)
                {
                    var startedAt = timer.ElapsedMilliseconds;
                    var resultStats = await renewCardResultRepository.GetStatsAsync(date);

                    var responseStats = new Ops.Models.Entities.RenewCardStats
                    {
                        CreatedAt = now,
                        Discarded = resultStats.DiscardedCount,
                        Month = date.Month,
                        Unprocessed = resultStats.NotProcessedCount,
                        Year = date.Year,
                    };

                    var responses = await renewCardResponseRepository.GetAllAsync();

                    foreach (var responseIdCount in resultStats.ResponseIdCount)
                    {
                        var response = responses.SingleOrDefault(_ => _.Id == responseIdCount.Key);

                        if (response == null)
                        {
                            logger.LogWarning(
                                "Unable to find response for response id {ResponseId}",
                                responseIdCount.Key);
                        }
                        else
                        {
                            switch (response.Type)
                            {
                                case Ops.Models.Entities.RenewCardResponse.ResponseType.Accept:
                                    responseStats.Accepted += responseIdCount.Value;
                                    break;
                                case Ops.Models.Entities.RenewCardResponse.ResponseType.Deny:
                                    responseStats.Denied += responseIdCount.Value;
                                    break;
                                case Ops.Models.Entities.RenewCardResponse.ResponseType.Partial:
                                    responseStats.Partial += responseIdCount.Value;
                                    break;
                                default:
                                    logger.LogWarning(
                                        "Unknown response type: {ResponseType}",
                                        response.Type);
                                    break;
                            }
                        }
                    }

                    await renewCardStatsRepository.SaveStatsAsync(responseStats);
                    logger.LogInformation(
                        "Finished running renew card stats report for {Month}/{Year} in {ElapsedMs} ms",
                        date.Month,
                        date.Year,
                        timer.ElapsedMilliseconds - startedAt);
                }

                logger.LogInformation(
                    "Finished all renew card stats reports in {ElapsedMs} ms",
                    timer.ElapsedMilliseconds);
            }
        }
    }
}
