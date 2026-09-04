using System.Xml.Serialization;

namespace Ocuda.Ops.Models.Communications
{
    // Property order intentionally matches the legacy Intranet PRIdmlModel because the
    // downstream InDesign workflow consumes the serialized PR.xml document.
    public class PrIdmlModel
    {
        public string Link { get; set; }
        public string Title { get; set; }
        public string Day { get; set; }
        public string Month { get; set; }
        public string Date { get; set; }
        public string Time { get; set; }

        [XmlElement(IsNullable = true)]
        public string EventLocation { get; set; }

        public string BranchName { get; set; }
        public string BranchCode { get; set; }
        public string Description { get; set; }
        public bool Registration { get; set; }
        public bool Ticketed { get; set; }
        public bool TicketPickUpDayOfEvent { get; set; }
        public int? TicketLimit { get; set; }

        [XmlElement(IsNullable = true)]
        public string Sponsor { get; set; }

        [XmlElement(IsNullable = true)]
        public string Studio { get; set; }

        [XmlElement(IsNullable = true)]
        public string ImageName { get; set; }

        [XmlElement(IsNullable = true)]
        public string ImageSource { get; set; }

        public int HalfSheet { get; set; }
        public int QuarterSheet { get; set; }
        public int Poster85x11 { get; set; }
        public int Poster11x17 { get; set; }
        public int Poster13x19 { get; set; }
        public int Poster18x24 { get; set; }
        public int Poster22x28 { get; set; }
        public int Poster24x36 { get; set; }
        public bool FlatScreen { get; set; }

        [XmlElement(IsNullable = true)]
        public string FlatScreenStart { get; set; }

        [XmlElement(IsNullable = true)]
        public string FlatScreenEnd { get; set; }

        public bool FacebookImage { get; set; }
        public bool HalfSheetImage { get; set; }
        public bool FullSheetImage { get; set; }

        [XmlElement(IsNullable = true)]
        public string SpecialRequests { get; set; }

        [XmlElement(IsNullable = true)]
        public string RequesterName { get; set; }

        [XmlElement(IsNullable = true)]
        public string RequesterEmail { get; set; }

        [XmlElement(IsNullable = true)]
        public string RequesterBranch { get; set; }

        public bool IsKid { get; set; }
        public bool IsTeen { get; set; }
        public string RequestType { get; set; }
        public string FileName { get; set; }
        public string ScreenlyIPs { get; set; }

        [XmlElement(IsNullable = true)]
        public string DisplaySetName { get; set; }

        public int? MediaTicketId { get; set; }
        public string TemplateName { get; set; }
        public bool Online { get; set; }
        public bool ShowInfoBox { get; set; }
    }
}
