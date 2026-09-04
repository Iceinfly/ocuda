using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.Encodings.Web;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Html;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Ocuda.Ops.Controllers.Abstract;
using Ocuda.Ops.Controllers.Areas.SiteManagement.ViewModels.Segment;
using Ocuda.Ops.Controllers.Filters;
using Ocuda.Ops.Models;
using Ocuda.Ops.Models.Entities;
using Ocuda.Ops.Models.Keys;
using Ocuda.Ops.Service.Filters;
using Ocuda.Ops.Service.Interfaces.Ops.Services;
using Ocuda.Ops.Service.Interfaces.Promenade.Services;
using Ocuda.Promenade.Models.Entities;
using Ocuda.Utility.Exceptions;
using Ocuda.Utility.Extensions;
using Ocuda.Utility.Filters;
using Ocuda.Utility.Keys;
using Ocuda.Utility.Models;

namespace Ocuda.Ops.Controllers.Areas.SiteManagement
{
    [Area("SiteManagement")]
    [Route("[area]/[controller]")]
    public class SegmentsController(ServiceFacades.Controller<SegmentsController> context,
        IEmediaService emediaService,
        IFeatureService featureService,
        ILanguageService languageService,
        ILocationFeatureService locationFeatureService,
        ILocationService locationService,
        IPermissionGroupService permissionGroupService,
        IPodcastService podcastService,
        IProductService productService,
        ISegmentService segmentService,
        ISegmentWrapService segmentWrapService,
        ISiteSettingPromService siteSettingPromService,
        IVolunteerFormService volunteerFormService) : BaseController<SegmentsController>(context)
    {
        private static readonly string[] SuppressHeadersForSiteSettings = [
            Promenade.Models.Keys.SiteSetting.Emedia.ButtonAllSegment,
            Promenade.Models.Keys.SiteSetting.Emedia.ButtonGroupSegment];

        public static string Area
        {
            get { return "SiteManagement"; }
        }

        public static string Name
        {
            get { return "Segments"; }
        }

        [Authorize(Policy = nameof(ClaimType.SiteManager))]
        [HttpPost]
        [Route("[action]")]
        public async Task<IActionResult> Create(IndexViewModel model)
        {
            if (model == null)
            {
                return Json(new JsonResponse
                {
                    Success = false,
                    Message = "Invalid request to create a segment.",
                });
            }

            if (model.SegmentStartDate.HasValue && model.SegmentStartTime.HasValue)
            {
                model.Segment.StartDate = model
                    .SegmentStartDate.Value.CombineWithTime(model.SegmentStartTime.Value);
            }

            if (model.SegmentEndDate.HasValue && model.SegmentEndTime.HasValue)
            {
                model.Segment.EndDate = model
                    .SegmentEndDate.Value.CombineWithTime(model.SegmentStartTime.Value);
            }

            if (model.Segment.StartDate.HasValue && model.Segment.EndDate.HasValue
                && model.Segment.StartDate > model.Segment.EndDate)
            {
                ModelState.AddModelError("Segment.StartDate",
                    "Start Date cannot be after the End Date.");
            }

            JsonResponse response;

            if (ModelState.IsValid)
            {
                try
                {
                    var segment = await segmentService.CreateAsync(model.Segment);
                    response = new JsonResponse
                    {
                        Success = true,
                        Url = Url.Action(nameof(Detail), new { id = segment.Id }),
                    };

                    ShowAlertSuccess($"Created segment: {segment.Name}");
                }
                catch (OcudaException ex)
                {
                    response = new JsonResponse
                    {
                        Success = false,
                        Message = ex.Message,
                    };
                }
            }
            else
            {
                var errors = ModelState.Values
                    .SelectMany(_ => _.Errors)
                    .Select(_ => _.ErrorMessage);

                response = new JsonResponse
                {
                    Success = false,
                    Message = string.Join(Environment.NewLine, errors),
                };
            }

            return Json(response);
        }

        [Authorize(Policy = nameof(ClaimType.SiteManager))]
        [HttpPost]
        [Route("[action]")]
        public async Task<IActionResult> Delete(IndexViewModel model)
        {
            ArgumentNullException.ThrowIfNull(model);

            try
            {
                await segmentService.DeleteAsync(model.Segment.Id);
                ShowAlertSuccess($"Deleted segment: {model.Segment.Name}");
            }
            catch (OcudaException ex)
            {
                var alertMessage = $"Unable to delete segment \"{model.Segment.Name}\": {ex.Message}";

                var inUseByList = (List<string>)ex.Data[OcudaExceptionData.SegmentInUseBy];
                if (inUseByList != null)
                {
                    var inUseByTag = new TagBuilder("ul");
                    foreach (var usedBy in inUseByList)
                    {
                        var inUseByItem = new TagBuilder("li");
                        inUseByItem.InnerHtml.SetContent(usedBy);
                        inUseByTag.InnerHtml.AppendHtml(inUseByItem);
                    }

                    await using var inUseByHtml = new StringWriter();
                    inUseByTag.WriteTo(inUseByHtml, HtmlEncoder.Default);
                    ShowAlertDanger($"{alertMessage} {inUseByHtml}");
                }
                else
                {
                    ShowAlertDanger(alertMessage);
                }
            }

            return RedirectToAction(nameof(Index), new { page = model.PaginateModel.CurrentPage });
        }

        [HttpPost]
        [Route("[action]")]
        public async Task<IActionResult> DeleteText(DetailViewModel model)
        {
            ArgumentNullException.ThrowIfNull(model);

            if (!await HasSegmentPermissionAsync(model.SegmentId))
            {
                return RedirectToUnauthorized();
            }

            var segmentText = await segmentService.GetBySegmentAndLanguageAsync(model.SegmentId,
                model.LanguageId);

            await segmentService.DeleteSegmentTextAsync(segmentText);

            var language = await languageService.GetActiveByIdAsync(model.LanguageId);

            ShowAlertSuccess($"Deleted Segment {language.Description} text!");

            return RedirectToAction(nameof(Detail),
                new
                {
                    id = model.SegmentId,
                    language = language.Name,
                });
        }

        [Obsolete("Please call the method with a culture (language name)")]
        [Route("[action]/{id}")]
        public async Task<IActionResult> Detail(int id)
        {
            return !await HasSegmentPermissionAsync(id)
                ? RedirectToUnauthorized()
                : RedirectToAction(nameof(Detail), new
                {
                    id,
                    language = await languageService.GetDefaultLanguageNameAsync(),
                });
        }

        [Route("[action]/{id}/{language}")]
        [RestoreModelState]
        public async Task<IActionResult> Detail(int id, string language)
        {
            if (!await HasSegmentPermissionAsync(id))
            {
                return RedirectToUnauthorized();
            }

            var segment = await segmentService.GetByIdAsync(id);
            if (segment == null)
            {
                ShowAlertDanger($"Could not find Segment with ID: {id}");
                return RedirectToAction(nameof(SegmentsController.Index));
            }

            var languages = await languageService.GetActiveAsync();

            var selectedLanguage = languages
                .FirstOrDefault(_ => _.Name.Equals(language, StringComparison.OrdinalIgnoreCase))
                ?? languages.Single(_ => _.IsDefault);

            var segmentText = await segmentService
                .GetBySegmentAndLanguageAsync(id, selectedLanguage.Id);

            var wrapList = await segmentWrapService.GetActiveListAsync();
            if (wrapList?.Count > 0)
            {
                wrapList.Add(string.Empty, "No wrap");
            }

            var viewModel = new DetailViewModel
            {
                IsActive = segment.IsActive,
                LanguageDescription = selectedLanguage.Description,
                LanguageId = selectedLanguage.Id,
                LanguageList = new SelectList(languages,
                    nameof(Language.Name),
                    nameof(Language.Description),
                    selectedLanguage.Name),
                SegmentEndDate = segment.EndDate,
                SegmentId = segment.Id,
                SegmentName = segment.Name,
                SegmentStartDate = segment.StartDate,
                SegmentText = await segmentService
                    .GetBySegmentAndLanguageAsync(id, selectedLanguage.Id),
                SegmentWrapId = segment.SegmentWrapId,
                SegmentWrapList = new SelectList(wrapList.OrderBy(_ => _.Key),
                    "Key",
                    "Value",
                    segment.SegmentWrapId),
            };

            viewModel.NewSegmentText = viewModel.SegmentText == null;

            // check if this segment is used elsewhere so we can contextualize the back button
            var pageLayoutId
                = await segmentService.GetPageLayoutIdForSegmentAsync(segment.Id);

            if (pageLayoutId.HasValue)
            {
                viewModel.BackLink = Url.Action(nameof(PagesController.LayoutDetail),
                    PagesController.Name,
                    new
                    {
                        id = pageLayoutId.Value,
                    });
                viewModel.Relationship
                    = $"This segment is used page layout ID: {pageLayoutId.Value}";
            }
            else
            {
                await PopulateRelationshipInformation(segment.Id, viewModel);
            }

            return View(viewModel);
        }

        [HttpPost("[action]/{id}/{language}")]
        [SaveModelState]
        public async Task<IActionResult> Detail(int id, string language, DetailViewModel model)
        {
            ArgumentNullException.ThrowIfNull(model);

            model.SegmentId = id;

            if (!await HasSegmentPermissionAsync(model.SegmentId))
            {
                return RedirectToUnauthorized();
            }

            if (string.IsNullOrWhiteSpace(model.SegmentText?.Header)
                && string.IsNullOrWhiteSpace(model.SegmentText?.Text))
            {
                ModelState.AddModelError("SegmentText.Text",
                    "You must supply text to save a segment.");
            }

            var languageObject = await languageService.GetActiveByCulture(language);

            if (ModelState.IsValid)
            {
                var segment = await segmentService.GetByIdAsync(model.SegmentId);
                if (segment != null)
                {
                    segment.IsActive = model.IsActive;
                    segment.SegmentWrapId = model.SegmentWrapId;
                    segment.StartDate = model.SegmentStartDate;
                    segment.EndDate = model.SegmentEndDate;
                    await segmentService.EditAsync(segment);
                }

                var segmentText = model.SegmentText;
                segmentText.LanguageId = languageObject.Id;
                segmentText.SegmentId = model.SegmentId;

                var currentSegmentText = await segmentService.GetBySegmentAndLanguageAsync(
                    model.SegmentId, languageObject.Id);

                if (currentSegmentText != null
                    && string.IsNullOrWhiteSpace(model.SegmentText.Text)
                    && string.IsNullOrWhiteSpace(model.SegmentText.Header))
                {
                    await segmentService.DeleteSegmentTextAsync(currentSegmentText);
                }
                else
                {
                    if (currentSegmentText == null)
                    {
                        await segmentService.CreateSegmentTextAsync(segmentText);
                        ShowAlertSuccess("Added segment text!");
                    }
                    else
                    {
                        await segmentService.EditSegmentTextAsync(segmentText);
                        ShowAlertSuccess("Updated segment text!");
                    }
                }

                // if this was an update to the name of a feature then update the name item as well
                var defaultLanguage = await languageService.GetDefaultLanguageId();
                if (languageObject.Id == defaultLanguage)
                {
                    var feature = await featureService.GetFeatureBySegmentIdAsync(segment.Id);
                    if (feature?.NameSegmentId == segment.Id)
                    {
                        await featureService.UpdateFeatureNameAsync(feature.Id, segmentText.Text);
                    }
                }
            }
            else
            {
                var sb = new StringBuilder("Unable to save, please correct the following issues:<ul>");
                foreach (var item in ModelState.Values)
                {
                    foreach (var error in item.Errors)
                    {
                        sb.Append("<li>")
                            .Append(error.ErrorMessage)
                            .Append("</li>");
                    }
                }

                sb.Append("</li>");
                ShowAlertDanger(sb.ToString());
            }

            return RedirectToAction(nameof(Detail), new
            {
                id = model.SegmentId,
                language = languageObject.Name,
            });
        }

        [Authorize(Policy = nameof(ClaimType.SiteManager))]
        [HttpPost]
        [Route("[action]")]
        [SaveModelState]
        public async Task<IActionResult> Edit(IndexViewModel model)
        {
            if (model == null)
            {
                return Json(new JsonResponse
                {
                    Success = false,
                    Message = "Invalid request to update a segment.",
                });
            }

            if (model.SegmentStartDate.HasValue && model.SegmentStartTime.HasValue)
            {
                model.Segment.StartDate = model
                    .SegmentStartDate.Value.CombineWithTime(model.SegmentStartTime.Value);
            }

            if (model.SegmentEndDate.HasValue && model.SegmentEndTime.HasValue)
            {
                model.Segment.EndDate = model
                    .SegmentEndDate.Value.CombineWithTime(model.SegmentStartTime.Value);
            }

            JsonResponse response;

            if (ModelState.IsValid)
            {
                try
                {
                    var segment = await segmentService.EditAsync(model.Segment);
                    response = new JsonResponse
                    {
                        Success = true,
                    };
                    ShowAlertSuccess($"Updated segment: {segment.Name}");
                }
                catch (OcudaException ex)
                {
                    response = new JsonResponse
                    {
                        Success = false,
                        Message = ex.Message,
                    };
                }
            }
            else
            {
                var errors = ModelState.Values
                    .SelectMany(_ => _.Errors)
                    .Select(_ => _.ErrorMessage);

                response = new JsonResponse
                {
                    Success = false,
                    Message = string.Join(Environment.NewLine, errors),
                };
            }

            return Json(response);
        }

        [Authorize(Policy = nameof(ClaimType.SiteManager))]
        [Route("")]
        [Route("[action]/{page}")]
        public async Task<IActionResult> Index(int page = 1)
        {
            var filter = new BaseFilter(page);
            var segmentList = await segmentService.GetPaginatedListAsync(filter);

            var paginateModel = new PaginateModel
            {
                ItemCount = segmentList.Count,
                CurrentPage = page,
                ItemsPerPage = filter.Take.Value,
            };
            if (paginateModel.PastMaxPage)
            {
                return RedirectToRoute(
                    new
                    {
                        page = paginateModel.LastPage ?? 1,
                    });
            }

            foreach (var segment in segmentList.Data.ToList())
            {
                segment.SegmentLanguages
                    = await segmentService.GetSegmentLanguagesByIdAsync(segment.Id);
            }

            var languages = await languageService.GetActiveAsync();
            var selectedLanguage = languages.Single(_ => _.IsDefault);
            var viewModel = new IndexViewModel
            {
                Segments = segmentList.Data,
                PaginateModel = paginateModel,
                LanguageList = new SelectList(languages,
                    nameof(Language.Id),
                    nameof(Language.Description),
                    selectedLanguage.Id),
                AvailableLanguages = [.. languages.Select(_ => _.Name)],
            };
            return View(viewModel);
        }

        private async Task<bool> HasSegmentPermissionAsync(int segmentId)
        {
            if (!string.IsNullOrEmpty(UserClaim(ClaimType.SiteManager))
                || await HasAppPermissionAsync(permissionGroupService,
                    ApplicationPermission.WebPageContentManagement))
            {
                return true;
            }
            else
            {
                var permissionClaims = UserClaims(ClaimType.PermissionId);
                if (permissionClaims.Count > 0)
                {
                    var pageHeaderId = await segmentService.GetPageHeaderIdForSegmentAsync(
                        segmentId);
                    if (pageHeaderId.HasValue)
                    {
                        var permissionGroups = await permissionGroupService
                            .GetPermissionsAsync<PermissionGroupPageContent>(pageHeaderId.Value);
                        var permissionGroupsStrings = permissionGroups
                            .Select(_ => _.PermissionGroupId
                                .ToString(CultureInfo.InvariantCulture));

                        return permissionClaims.Any(_ => permissionGroupsStrings.Contains(_));
                    }

                    var emediaGroup = await emediaService.GetGroupUsingSegmentAsync(segmentId);
                    if (emediaGroup != null)
                    {
                        return await HasAppPermissionAsync(permissionGroupService,
                            ApplicationPermission.EmediaManagement);
                    }

                    var podcast = await podcastService.GetEpisodeBySegmentIdAsync(segmentId);
                    if (podcast != null)
                    {
                        return await HasPermissionAsync<PermissionGroupPodcastItem>(
                            permissionGroupService, podcast.PodcastId)
                            && await HasAppPermissionAsync(permissionGroupService,
                                ApplicationPermission.PodcastShowNotesManagement);
                    }
                }

                return false;
            }
        }

        private async Task PopulateRelationshipInformation(int segmentId, DetailViewModel viewModel)
        {
            var emediaGroup = await emediaService.GetGroupUsingSegmentAsync(segmentId);
            if (emediaGroup != null)
            {
                viewModel.BackLink = Url.Action(nameof(EmediaController.GroupDetails),
                    EmediaController.Name,
                    new
                    {
                        id = emediaGroup.Id,
                    });
                viewModel.Relationship
                    = $"This segment is used by emedia group: {emediaGroup.Name}";
                return;
            }

            var feature = await featureService.GetFeatureBySegmentIdAsync(segmentId);
            if (feature != null)
            {
                viewModel.BackLink = Url.Action(nameof(FeaturesController.Feature),
                    FeaturesController.Name,
                    new
                    {
                        area = FeaturesController.Area,
                        slug = feature.Stub,
                    });
                viewModel.Relationship = $"This segment is used for feature: {feature.Name}";
            }

            var locations = await locationService.GetLocationsBySegment(segmentId);
            if (locations?.Count == 1)
            {
                viewModel.BackLink = Url.Action(nameof(Controllers.LocationsController.Details),
                    Controllers.LocationsController.Name,
                    new
                    {
                        area = string.Empty,
                        slug = locations.First().Stub,
                    });

                if (locations.First().DescriptionSegmentId == segmentId)
                {
                    viewModel.Relationship
                        = $"This segment is used as the description of location: {locations.First().Name}";
                    viewModel.SuppressHeader = true;
                    viewModel.SuppressWrap = true;
                }
                else
                {
                    viewModel.CanBeDeactivated = true;
                    viewModel.IsSchedulable = true;
                    viewModel.SuppressHeader = true;

                    if (locations.First().PreFeatureSegmentId == segmentId)
                    {
                        viewModel.FlagWrap
                            = "You may wish to set a wrap for this location-based notice displayed at the top of the page.";
                        viewModel.Relationship
                            = $"This segment is shown at the top of the page for location: {locations.First().Name}";
                    }
                    else if (locations.First().PostFeatureSegmentId == segmentId)
                    {
                        viewModel.SuppressWrap = true;
                        viewModel.Relationship
                            = $"This segment is shown below the hours for: {locations.First().Name}";
                    }
                    else if (locations.First().HoursSegmentId == segmentId)
                    {
                        viewModel.SuppressWrap = true;
                        viewModel.Relationship
                            = $"This segment is shown in place of calculated hours for: {locations.First().Name}";
                    }
                }

                return;
            }

            if (locations?.Count > 1)
            {
                viewModel.Relationship = string.Format(CultureInfo.InvariantCulture,
                    "This segment is used for multiple locations: {0}",
                    string.Join(", ", locations.Select(_ => _.Name)));
                return;
            }

            var locationFeature = await locationFeatureService
                .GetLocationFeatureBySegmentIdAsync(segmentId);
            if (locationFeature != null)
            {
                var location = await locationService
                    .GetLocationByIdAsync(locationFeature.LocationId);
                viewModel.BackLink
                    = Url.Action(nameof(Controllers.LocationsController.LocationFeature),
                        Controllers.LocationsController.Name,
                        new
                        {
                            area = string.Empty,
                            slug = location.Stub,
                            featureId = locationFeature.FeatureId,
                        });
                viewModel.Relationship = "This segment is used to customize a location feature description.";
                return;
            }

            var episode = await podcastService.GetEpisodeBySegmentIdAsync(segmentId);
            if (episode != null)
            {
                viewModel.BackLink = Url.Action(nameof(PodcastsController.EditEpisode),
                    PodcastsController.Name,
                    new
                    {
                        episodeId = episode.Id,
                    });
                viewModel.Relationship
                    = $"This segment is used for podcast '{episode.Podcast.Title}' episode #{episode.Episode.Value}";
                string published = episode.PublishDate.HasValue
                    ? $"published {episode.PublishDate.Value:D}"
                    : "not yet published";
                viewModel.AutomatedHeaderMarkup
                    = $"<strong>Show notes for {episode.Title}</strong><br>{episode.Podcast.Title}. <em>Episode {episode.Episode}, {published}.</em>";
                viewModel.IsShowNotes = episode != null;
                return;
            }

            var forms = await volunteerFormService.GetFormBySegmentIdAsync(segmentId);
            if (forms?.Count == 1)
            {
                viewModel.BackLink = Url.Action(nameof(VolunteerController.Form),
                    VolunteerController.Name,
                    new
                    {
                        id = forms.First().Id,
                    });
                viewModel.Relationship
                    = $"This segment is used for form type: {forms.First().VolunteerFormType}";
                viewModel.TemplateFields.Add(Template.LocationName);
                return;
            }

            var products = await productService.GetBySegmentIdAsync(segmentId);
            if (products?.Count == 1)
            {
                viewModel.BackLink = Url.Action(nameof(ProductsController.Details),
                    ProductsController.Name,
                    new
                    {
                        productSlug = products.First().Slug,
                    });
                viewModel.Relationship
                    = $"This segment is used for product: {products.First().Name}";
                viewModel.AutomatedHeaderMarkup
                    = $"<strong>{products.First().Name}</strong>";
                return;
            }
            else if (products?.Count > 1)
            {
                viewModel.Relationship = string.Format(CultureInfo.InvariantCulture,
                    "This segment is used for multiple products: {0}",
                    string.Join(", ", products.Select(_ => _.Name)));
                return;
            }

            var promSettings = await siteSettingPromService.GetAllAsync();
            var segmentSetting = promSettings.FirstOrDefault(_ => _.Id.EndsWith("Segment")
                && _.Value == segmentId.ToString(CultureInfo.InvariantCulture));

            if (segmentSetting != null)
            {
                viewModel.Relationship
                    = $"This segment is used for site setting: {segmentSetting.Name}";
                viewModel.SuppressHeader = SuppressHeadersForSiteSettings
                    .Contains(segmentSetting.Id);
                viewModel.SuppressWrap = true;

                switch (segmentSetting.Category)
                {
                    case nameof(Promenade.Models.Keys.SiteSetting.Emedia):
                        viewModel.BackLink = Url.Action(nameof(EmediaController.Configure),
                            EmediaController.Name,
                            new { area = EmediaController.Area });
                        break;
                }
            }
        }
    }
}
