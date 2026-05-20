using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using Ocuda.Ops.Controllers.Abstract;
using Ocuda.Ops.Controllers.Areas.Reporting.ViewModel;
using Ocuda.Ops.Controllers.ServiceFacades;
using Ocuda.Ops.Models.Definitions;
using Ocuda.Ops.Models.Entities;
using Ocuda.Ops.Models.Keys;
using Ocuda.Ops.Service.Filters;
using Ocuda.Ops.Service.Interfaces.Ops.Services;
using Ocuda.Utility.Exceptions;
using Ocuda.Utility.Extensions;
using Serilog.Context;

namespace Ocuda.Ops.Controllers.Areas.Reporting
{
    [Area(nameof(Reporting))]
    [Route("[area]")]
    public class HomeController(Controller<HomeController> context,
        IPermissionGroupService permissionGroupService,
        IReportingService reportingService,
        IUserService userService)
        : BaseController<HomeController>(context)
    {
        public static string Area
        { get { return nameof(Reporting); } }

        public static string Name
        { get { return "Home"; } }

        [HttpGet("[action]/{reportId}")]
        public async Task<IActionResult> Details(string reportId, int page)
        {
            var currentPage = page > 0 ? page : 1;

            var itemsPerPage = await _siteSettingService
                .GetSettingIntAsync(Models.Keys.SiteSetting.UserInterface.ItemsPerPage);

            var report = ReportDefinitions.Definitions.SingleOrDefault(_ => _.Id == reportId);

            if (report == null)
            {
                ShowAlertWarning($"Unable to find report with id: {reportId}");
                return RedirectToAction(nameof(Index));
            }

            var filter = new BaseFilter<string>(currentPage, itemsPerPage)
            {
                Data = report.Id
            };

            var collectionWithCount = await reportingService.GetResultsAsync(filter);

            var viewModel = new DetailsViewModel
            {
                CurrentPage = currentPage,
                Heading = $"Results for {report.Name}",
                ItemCount = collectionWithCount.Count,
                ItemsPerPage = filter.Take.Value,
                Report = report
            };

            viewModel.Dates.AddRange(collectionWithCount.Data);

            if (viewModel.PastMaxPage)
            {
                return RedirectToRoute(new { currentPage = viewModel.LastPage ?? 1 });
            }

            SetPageTitle(viewModel.Heading);
            return View(viewModel);
        }

        [HttpPost("[action]")]
        public async Task<IActionResult> Import(ImportViewModel viewModel)
        {
            if (viewModel == null)
            {
                ShowAlertDanger("Unable to accept uploaded data file.");
                return RedirectToAction(nameof(Import));
            }

            var report = ReportDefinitions
                .Definitions
                .SingleOrDefault(_ => _.Id == viewModel.ReportId);
            if (report == null)
            {
                ShowAlertWarning($"Unable to find report with id: {viewModel.ReportId}");
                return RedirectToAction(nameof(Index));
            }

            if (!await HasImportPermissionAsync(report.Id))
            {
                return RedirectToUnauthorized();
            }

            // TODO REPORT: see if data exists for the selected date, if so prompt or overwrite?

            await using var stream = viewModel.DataFile.OpenReadStream();

            using (LogContext.PushProperty("ImportFilename", viewModel.DataFile.FileName))
            {
                var importResult = await reportingService.ProcessImportAsync(report.Id,
                    viewModel.DataDate,
                    viewModel.DataFile.FileName,
                    stream);
                return RedirectToAction(nameof(Notes), new
                {
                    reportId = report.Id,
                    year = viewModel.DataDate.Year,
                    month = viewModel.DataDate.Month
                });
            }
        }

        //[HttpGet("[action]/{reportId}")]
        //public async Task<IActionResult> Import(string reportId)
        //{
        //    var viewModel = new ImportViewModel
        //    {
        //        DataDate = new DateTime(DateTime.Now.Year, DateTime.Now.Month, 1).AddMonths(-1),
        //        Report = ReportDefinitions.Definitions.SingleOrDefault(_ => _.Id == reportId)
        //    };

        //    if (viewModel.Report == null)
        //    {
        //        ShowAlertWarning($"Unable to find report with id: {reportId}");
        //        return RedirectToAction(nameof(Index));
        //    }

        //    if (!await HasImportPermissionAsync(viewModel.Report.Id))
        //    {
        //        return RedirectToUnauthorized();
        //    }

        //    viewModel.Heading = $"Import data for {viewModel.Report.Name}";
        //    SetPageTitle(viewModel.Heading);
        //    return View(viewModel);
        //}

        [HttpGet("")]
        public async Task<IActionResult> Index(int page)
        {
            var currentPage = page > 0 ? page : 1;

            var itemsPerPage = await _siteSettingService
                .GetSettingIntAsync(Models.Keys.SiteSetting.UserInterface.ItemsPerPage);

            var filter = new BaseFilter(currentPage, itemsPerPage);

            var collectionWithCount = reportingService.GetList(filter);

            var viewModel = new IndexViewModel
            {
                CurrentPage = currentPage,
                Heading = "Reporting",
                IsIndex = true,
                ItemCount = collectionWithCount.Count,
                ItemsPerPage = filter.Take.Value,
                Reports = collectionWithCount.Data,
            };

            if (viewModel.PastMaxPage)
            {
                return RedirectToRoute(new { page = viewModel.LastPage ?? 1 });
            }

            // loop through reports to see if the current user can import for them
            foreach (var report in viewModel.Reports)
            {
                report.Importable = await HasImportPermissionAsync(report.Id);
                report.HasResults = await reportingService.HasResultsAsync(report.Id);
            }

            SetPageTitle(viewModel.Heading);
            return View(viewModel);
        }

        [Route("[action]/{reportId}/{year}/{month}")]
        public async Task<IActionResult> Notes(string reportId, int year, int month)
        {
            var reportType = ReportDefinitions.Definitions.SingleOrDefault(_ => _.Id == reportId);

            if (reportType == null) { return StatusCode(404); }

            IEnumerable<ReportingImportDetails> notes = null;

            try
            {
                notes = await reportingService.GetNotesAsync(reportId, year, month);
            }
            catch (OcudaException oex)
            {
                _logger.LogError(oex,
                    "Request for notes not found: {ReportId} {Month}/{Year}",
                    reportId,
                    month,
                    year);
                return StatusCode(404);
            }

            var viewModel = new NotesViewModel
            {
                BackLink = Url.Action(nameof(Details), new { reportId }),
                Heading = "Import Notes",
                Notes = notes,
                SecondaryHeading = $"{reportType.Name} {month}/{year}"
            };

            viewModel.Navigations.Add("Results",
                Url.Action(nameof(Results), new { reportId, year, month }));

            var userCache = new Dictionary<int, User>();
            foreach (var note in viewModel.Notes)
            {
                if (userCache.TryGetValue(note.CreatedBy, out var user))
                {
                    note.CreatedByUser = user;
                }
                else
                {
                    note.CreatedByUser = await userService
                        .GetByIdIncludeDeletedAsync(note.CreatedBy);
                    userCache.Add(note.CreatedBy, note.CreatedByUser);
                }
            }

            SetPageTitle($"{viewModel.Heading} - {viewModel.SecondaryHeading}");
            return View(viewModel);
        }

        [Route("[action]/{reportId}/{year}/{month}")]
        public async Task<IActionResult> Results(string reportId, int year, int month)
        {
            var reportType = ReportDefinitions.Definitions.SingleOrDefault(_ => _.Id == reportId);

            if (reportType == null) { return StatusCode(404); }

            var results = await reportingService.GetResultsAsync(reportType.Id, year, month);

            if (results == null) { return StatusCode(404); }

            var viewModel = new ResultsViewModel
            {
                BackLink = Url.Action(nameof(Details), new { reportId }),
                Heading = reportType.Name,
                Results = results,
                SecondaryHeading = $"{results.Month}/{results.Year}"
            };

            viewModel.Navigations.Add("Import Notes",
                Url.Action(nameof(Notes), new { reportId, year, month }));

            SetPageTitle(viewModel.Heading);
            return View(viewModel);
        }

        private async Task<bool> HasImportPermissionAsync(string reportId)
        {
            // TODO REPORT add permissions based on report the way we do with pages
            // TODO REPORT add permission check for if user can view report(s)
            return await HasAppPermissionAsync(permissionGroupService,
                ApplicationPermission.ImportAllReports)
                || IsSiteManager();
        }
    }
}