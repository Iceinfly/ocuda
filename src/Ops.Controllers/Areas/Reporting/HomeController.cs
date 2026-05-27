using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using Ocuda.Models;
using Ocuda.Ops.Controllers.Abstract;
using Ocuda.Ops.Controllers.Areas.Reporting.ViewModel;
using Ocuda.Ops.Controllers.ServiceFacades;
using Ocuda.Ops.Models;
using Ocuda.Ops.Models.Definitions;
using Ocuda.Ops.Models.Definitions.Models;
using Ocuda.Ops.Models.Entities;
using Ocuda.Ops.Models.Keys;
using Ocuda.Ops.Service.Filters;
using Ocuda.Ops.Service.Interfaces.Ops.Services;
using Ocuda.Utility.Exceptions;
using Ocuda.Utility.Extensions;
using Ocuda.Utility.Helpers;
using Ocuda.Utility.Keys;
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

        [Authorize(Policy = nameof(ClaimType.SiteManager))]
        [HttpPost("[action]/{reportId}/{permissionGroupId}")]
        public async Task<IActionResult> AddPermissionGroup(string reportId, int permissionGroupId)
        {
            var report = ReportDefinitions.Definitions.SingleOrDefault(_ => _.Id == reportId);
            if (report == null) { return StatusCode(404); }

            try
            {
                await permissionGroupService.AddToPermissionGroupAsync<PermissionGroupReporting>(
                    report.InternalId,
                    permissionGroupId);
                AlertInfo = "Permission to access report added.";
            }
            catch (OcudaException oex)
            {
                ShowAlertDanger($"Error updating permission: {oex.Message}");
            }

            return RedirectToAction(nameof(Permissions), new { reportId });
        }

        [HttpGet("[action]")]
        public IActionResult Definition(string reportId)
        {
            if (string.IsNullOrEmpty(reportId)) { return StatusCode(404); }
            var report = ReportDefinitions.Definitions.SingleOrDefault(_ => _.Id == reportId);
            if (report == null) { return StatusCode(404); }

            return View(report);
        }

        [HttpGet("[action]/{reportId}")]
        public async Task<IActionResult> Details(string reportId, int page)
        {
            var currentPage = page > 0 ? page : 1;

            var itemsPerPage = await _siteSettingService
                .GetSettingIntAsync(Models.Keys.SiteSetting.UserInterface.ItemsPerPage);

            var report = ReportDefinitions.Definitions.SingleOrDefault(_ => _.Id == reportId);
            if (report == null) { return StatusCode(404); }

            var permissions = await GetPermissionsAsync(report);
            if (!permissions.CanView) { return RedirectToUnauthorized(); }

            report.IsImportable = permissions.CanImport;

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

            viewModel.MonthsTotals.AddRange(collectionWithCount.Data);

            if (report.IsImportable)
            {
                viewModel.Navigations.Add("Import Data", "import");
            }

            if (viewModel.PastMaxPage)
            {
                return RedirectToRoute(new { currentPage = viewModel.LastPage ?? 1 });
            }

            SetPageTitle(viewModel.Heading);
            return View(viewModel);
        }

        [HttpGet("[action]/{reportId}/{year}/{month}")]
        public async Task<IActionResult> Display(string reportId, int year, int month)
        {
            var report = ReportDefinitions.Definitions.SingleOrDefault(_ => _.Id == reportId);
            if (report == null) { return StatusCode(404); }

            var permissions = await GetPermissionsAsync(report);
            if (!permissions.CanView) { return RedirectToUnauthorized(); }

            IEnumerable<DisplayReport> results = null;
            try
            {
                results = await reportingService.GetResultsAsync(report.Id, year, month, "N0");
            }
            catch (OcudaException oex)
            {
                _logger.LogWarning(oex, "Report display failed: {ErrorMessage}", oex.Message);
            }

            if (results?.Count() > 0)
            {
                var viewModel = new DisplayViewModel
                {
                    BackLink = Url.Action(nameof(Details), new { reportId }),
                    Heading = report.Name,
                    Reports = results,
                    SecondaryHeading = $"{month}/{year}"
                };

                if (permissions.CanImport)
                {
                    viewModel.Navigations.Add("Import Notes",
                        Url.Action(nameof(Notes), new { reportId, year, month }));
                }

                viewModel.Navigations.Add(
                    "Export",
                    Url.Action(nameof(Export), new { reportId, year, month }));

                SetPageTitle(viewModel.Heading);
                return View(viewModel);
            }

            return StatusCode(404);
        }

        [HttpGet("[action]/{reportId}/{year}/{month}")]
        public async Task<IActionResult> Export(string reportId, int year, int month)
        {
            var report = ReportDefinitions.Definitions.SingleOrDefault(_ => _.Id == reportId);
            if (report == null) { return StatusCode(404); }

            var permissions = await GetPermissionsAsync(report);
            if (!permissions.CanView) { return RedirectToUnauthorized(); }

            IEnumerable<DisplayReport> results = null;
            try
            {
                results = await reportingService.GetResultsAsync(report.Id, year, month);
            }
            catch (OcudaException oex)
            {
                _logger.LogWarning(oex, "Report display failed: {ErrorMessage}", oex.Message);
            }

            if (results?.Count() > 0)
            {
                results.First().Title = report.Name;
                var ms = ExcelExportHelper.GenerateWorkbook(results,
                    new Dictionary<string, object>
                    {
                        {"Report name", report.Name },
                        {"Type", report.ReportType },
                        {"Period", report.Period },
                        {"Timeframe", $"{month:D2}/{year:D4}" }
                    },
                    ExcelExportHelper.SheetCriteriaDefault);
                return new FileStreamResult(ms, ExcelExportHelper.ExcelMimeType)
                {
                    FileDownloadName = $"{report.Name.Replace(' ', '-')}-{year:D4}-{month:D2}.{ExcelExportHelper.ExcelFileExtension}"
                };
            }

            return StatusCode(404);
        }

        [HttpGet("[action]")]
        public async Task<IActionResult> HasResults(string reportId, int year, int month)
        {
            var jsonResponse = new JsonResponse
            {
                ServerResponse = true
            };

            var report = ReportDefinitions.Definitions.SingleOrDefault(_ => _.Id == reportId);

            if (report == null)
            {
                jsonResponse.Message = "Report not found.";
            }
            else
            {
                var permissions = await GetPermissionsAsync(report);

                if (!permissions.CanView)
                {
                    jsonResponse.Message = "Permission denied";
                }
                else
                {
                    jsonResponse.Success = await reportingService
                        .HasResultsAsync(report.Id, year, month);
                    jsonResponse.Message = jsonResponse.Success
                        ? $"Data already present for {month}/{year}."
                        : $"No data for {month}/{year}, good to upload!";
                }
            }

            return Json(jsonResponse);
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

            var permissions = await GetPermissionsAsync(report);

            if (!permissions.CanImport)
            {
                return RedirectToUnauthorized();
            }

            await using var stream = viewModel.DataFile.OpenReadStream();

            using (LogContext.PushProperty("ImportFilename", viewModel.DataFile.FileName))
            {
                try
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
                catch (OcudaException oex)
                {
                    _logger.LogWarning(oex,
                        "Unable to process import: {ErrorMessage}",
                        oex.Message);
                    ShowAlertWarning(oex.Message);
                    return RedirectToAction(nameof(Index));
                }
            }
        }

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
                CanAdjustPermissions = IsSiteManager(),
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
                var permissions = await GetPermissionsAsync(report);

                report.HasResults = await reportingService.HasResultsAsync(report.Id);
                report.IsImportable = permissions.CanImport;
                report.IsViewable = permissions.CanView;
            }

            SetPageTitle(viewModel.Heading);
            return View(viewModel);
        }

        [HttpGet("[action]/{reportId}/{year}/{month}")]
        public async Task<IActionResult> Notes(string reportId, int year, int month)
        {
            var report = ReportDefinitions.Definitions.SingleOrDefault(_ => _.Id == reportId);
            if (report == null) { return StatusCode(404); }

            var permissions = await GetPermissionsAsync(report);
            if (!permissions.CanImport) { return RedirectToUnauthorized(); }

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
                SecondaryHeading = $"{report.Name} {month}/{year}"
            };

            viewModel.Navigations.Add("Display",
                Url.Action(nameof(Display), new { reportId, year, month }));

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

        [Authorize(Policy = nameof(ClaimType.SiteManager))]
        [HttpGet("[action]/{reportId}")]
        public async Task<IActionResult> Permissions(string reportId)
        {
            var report = ReportDefinitions.Definitions.SingleOrDefault(_ => _.Id == reportId);
            if (report == null) { return StatusCode(404); }

            var viewModel = new PermissionsViewModel
            {
                Heading = "Permissions",
                Report = report,
                SecondaryHeading = report.Name
            };

            var permissionGroups = await permissionGroupService.GetAllAsync();
            var reportPermissions = await permissionGroupService
                .GetPermissionsAsync<PermissionGroupReporting>(report.InternalId);

            foreach (var permissionGroup in permissionGroups)
            {
                var permission = reportPermissions
                    .SingleOrDefault(_ => _.PermissionGroupId == permissionGroup.Id);
                if (permission == null)
                {
                    viewModel.AvailableGroups.Add(permissionGroup.Id,
                        permissionGroup.PermissionGroupName);
                }
                else
                {
                    viewModel.AssignedGroups.Add(permissionGroup.Id,
                        permissionGroup.PermissionGroupName);
                    if (permission.CanImport)
                    {
                        viewModel.ImportGroups.Add(permissionGroup.Id,
                            permissionGroup.PermissionGroupName);
                    }
                }
            }

            SetPageTitle($"{viewModel.Heading} - {viewModel.SecondaryHeading}");
            return View(viewModel);
        }

        [Authorize(Policy = nameof(ClaimType.SiteManager))]
        [HttpPost("[action]/{reportId}/{permissionGroupId}")]
        public async Task<IActionResult> RemovePermissionGroup(string reportId,
            int permissionGroupId)
        {
            var report = ReportDefinitions.Definitions.SingleOrDefault(_ => _.Id == reportId);
            if (report == null) { return StatusCode(404); }

            try
            {
                await permissionGroupService
                    .RemoveFromPermissionGroupAsync<PermissionGroupReporting>(report.InternalId,
                        permissionGroupId);
                AlertInfo = "Permission to access report removed.";
            }
            catch (OcudaException oex)
            {
                ShowAlertDanger($"Error updating permission: {oex.Message}");
            }

            return RedirectToAction(nameof(Permissions), new { reportId });
        }

        [Authorize(Policy = nameof(ClaimType.SiteManager))]
        [HttpPost("[action]/{reportId}/{permissionGroupId}")]
        public async Task<IActionResult> ToggleImportPermission(string reportId,
            int permissionGroupId)
        {
            var report = ReportDefinitions.Definitions.SingleOrDefault(_ => _.Id == reportId);
            if (report == null) { return StatusCode(404); }

            var permissions = await permissionGroupService
                .GetPermissionsAsync<PermissionGroupReporting>(report.InternalId);

            var permission = permissions.Single(_ => _.PermissionGroupId == permissionGroupId);
            permission.CanImport = !permission.CanImport;

            permissionGroupService.UpdatePermissionGroup<PermissionGroupReporting>(permission);
            await permissionGroupService.SavePermissionGroups();

            return RedirectToAction(nameof(Permissions), new { reportId = report.Id });
        }

        /// <summary>
        /// Return the current user's permissions in reference to the supplied
        /// <see cref="ReportDefinition"/>.
        /// </summary>
        /// <param name="report">The report to look up permissions for</param>
        /// <returns>A populated <see cref="ReportPermission"/> object</returns>
        private async Task<ReportPermission> GetPermissionsAsync(ReportDefinition report)
        {
            if (await HasAppPermissionAsync(permissionGroupService,
                ApplicationPermission.ImportAllReports) || IsSiteManager())
            {
                return new ReportPermission
                {
                    CanImport = true,
                    CanView = true
                };
            }

            var perms = await permissionGroupService
                .GetPermissionsAsync<PermissionGroupReporting>(report.InternalId);

            return new ReportPermission
            {
                CanImport = perms?.Any(_ => _.CanImport) == true,
                CanView = perms?.Count > 0
            };
        }

        /// <summary>
        /// Contains information about whether the current user can view a report or import data
        /// </summary>
        private class ReportPermission
        {
            public bool CanImport { get; set; }
            public bool CanView { get; set; }
        }
    }
}