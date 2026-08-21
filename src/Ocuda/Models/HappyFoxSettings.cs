namespace Ocuda.Models
{
    public class HappyFoxSettings
    {
        public static readonly string SectionName = "HappyFoxSettings";

        public string ApiKey { get; set; }
        public string AuthCode { get; set; }
        public string BaseUrl { get; set; }
        public int StaffId { get; set; }
    }
}
