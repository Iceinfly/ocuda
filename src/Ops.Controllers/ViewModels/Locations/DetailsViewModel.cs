using System;
using System.Collections.Generic;
using Ocuda.Ops.Controllers.Areas.SiteManagement.ViewModels.Location;
using Ocuda.Ops.Models.Entities;
using Ocuda.Promenade.Models.Entities;

namespace Ocuda.Ops.Controllers.ViewModels.Locations
{
    public class DetailsViewModel
    {
        public DetailsViewModel()
        {
            AllLanguages = new Dictionary<int, string>();
            DescriptionLanguages = [];
            LocationNoticeLanguages = [];
            HoursReplacementNoticeLanguages = [];
            PostHoursNoticeLanguages = [];
            VolunteerForms = [];
        }

        public static string Now
        {
            get
            {
                return DateTime.Now.ToString("s");
            }
        }

        public string ActiveLocationNoticeBorderCssClass
        {
            get
            {
                return !LocationNoticeSegment.IsActive
                    || LocationNoticeIsBeforeStart
                    || LocationNoticeIsAfterEnd
                        ? "border-danger"
                        : "border-success";
            }
        }

        public string ActiveLocationNoticeCssClass
        {
            get
            {
                return !LocationNoticeSegment.IsActive
                    || LocationNoticeIsBeforeStart
                    || LocationNoticeIsAfterEnd
                        ? "text-danger"
                        : "text-success";
            }
        }

        public string ActiveHoursReplacementNoticeBorderCssClass
        {
            get
            {
                return !HoursReplacementNoticeSegment.IsActive
                    || HoursReplacementNoticeIsBeforeStart
                    || HoursReplacementNoticeIsAfterEnd
                        ? "border-danger"
                        : "border-success";
            }
        }

        public string ActiveHoursReplacementNoticeCssClass
        {
            get
            {
                return !HoursReplacementNoticeSegment.IsActive
                    || HoursReplacementNoticeIsBeforeStart
                    || HoursReplacementNoticeIsAfterEnd
                        ? "text-danger"
                        : "text-success";
            }
        }

        public string ActivePostHoursNoticeBorderCssClass
        {
            get
            {
                return !PostHoursNoticeSegment.IsActive
                    || PostHoursNoticeIsBeforeStart
                    || PostHoursNoticeIsAfterEnd
                        ? "border-danger"
                        : "border-success";
            }
        }

        public string ActivePostHoursNoticeCssClass
        {
            get
            {
                return !PostHoursNoticeSegment.IsActive
                    || PostHoursNoticeIsBeforeStart
                    || PostHoursNoticeIsAfterEnd
                        ? "text-danger"
                        : "text-success";
            }
        }

        public IDictionary<int, string> AllLanguages { get; }

        public IEnumerable<Feature> AtThisLocation { get; set; }

        public ICollection<string> DescriptionLanguages { get; }

        public IEnumerable<DigitalDisplay> Displays { get; set; }

        public bool IsSiteManager { get; set; }

        public Location Location { get; set; }

        public bool LocationManager { get; set; }

        public ICollection<string> LocationNoticeLanguages { get; }

        public Segment LocationNoticeSegment { get; set; }

        public ICollection<string> PostHoursNoticeLanguages { get; }

        public Segment PostHoursNoticeSegment { get; set; }

        public ICollection<string> HoursReplacementNoticeLanguages { get; }

        public Segment HoursReplacementNoticeSegment { get; set; }

        public string HoursReplacementNoticeStatus
        {
            get
            {
                return !HoursReplacementNoticeSegment.IsActive
                    ? "Disabled"
                        : HoursReplacementNoticeIsBeforeStart
                        ? $"Starts {HoursReplacementNoticeSegment.StartDate}"
                        : HoursReplacementNoticeIsAfterEnd
                            ? $"Ended {HoursReplacementNoticeSegment.EndDate}"
                            : "Live";
            }
        }

        public string PostHoursNoticeStatus
        {
            get
            {
                return !PostHoursNoticeSegment.IsActive
                    ? "Disabled"
                        : LocationNoticeIsBeforeStart
                        ? $"Starts {PostHoursNoticeSegment.StartDate}"
                        : LocationNoticeIsAfterEnd
                            ? $"Ended {PostHoursNoticeSegment.EndDate}"
                            : "Live";
            }
        }

        public string LocationNoticeStatus
        {
            get
            {
                return !LocationNoticeSegment.IsActive
                    ? "Disabled"
                        : LocationNoticeIsBeforeStart
                        ? $"Starts {LocationNoticeSegment.StartDate}"
                        : LocationNoticeIsAfterEnd
                            ? $"Ended {LocationNoticeSegment.EndDate}"
                            : "Live";
            }
        }

        public bool SegmentEditor { get; set; }

        public IEnumerable<Feature> ServicesAvailable { get; set; }

        public ICollection<LocationVolunteerFormViewModel> VolunteerForms { get; }

        private bool HoursReplacementNoticeIsAfterEnd
        {
            get
            {
                return HoursReplacementNoticeSegment?.EndDate.HasValue == true
                    && HoursReplacementNoticeSegment.EndDate <= DateTime.Now;
            }
        }

        private bool HoursReplacementNoticeIsBeforeStart
        {
            get
            {
                return HoursReplacementNoticeSegment?.StartDate.HasValue == true
                    && HoursReplacementNoticeSegment.StartDate >= DateTime.Now;
            }
        }

        private bool LocationNoticeIsAfterEnd
        {
            get
            {
                return LocationNoticeSegment?.EndDate.HasValue == true
                    && LocationNoticeSegment.EndDate <= DateTime.Now;
            }
        }

        private bool LocationNoticeIsBeforeStart
        {
            get
            {
                return LocationNoticeSegment?.StartDate.HasValue == true
                    && LocationNoticeSegment.StartDate >= DateTime.Now;
            }
        }

        private bool PostHoursNoticeIsAfterEnd
        {
            get
            {
                return PostHoursNoticeSegment?.EndDate.HasValue == true
                    && PostHoursNoticeSegment.EndDate <= DateTime.Now;
            }
        }

        private bool PostHoursNoticeIsBeforeStart
        {
            get
            {
                return PostHoursNoticeSegment?.StartDate.HasValue == true
                    && PostHoursNoticeSegment.StartDate >= DateTime.Now;
            }
        }

        public static string LanguagesTitle(ICollection<string> languages)
        {
            return languages == null
                ? "Not available in any languages."
                : languages.Count == 0
                ? $"Available in {languages.Count} languages."
                : languages.Count == 1
                    ? $"Available in {languages.Count} language: {string.Join(", ", languages)}"
                    : $"Available in {languages.Count} languages:  {string.Join(", ", languages)}";
        }

        public string LanguagesCssClass(ICollection<string> languages)
        {
            return languages != null && languages?.Count == AllLanguages.Count
                ? "text-success"
                : "text-warning";
        }
    }
}
