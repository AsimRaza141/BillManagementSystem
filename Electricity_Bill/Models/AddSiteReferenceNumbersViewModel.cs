namespace Electricity_Bill.Models
{
    public class AddSiteReferenceNumbersViewModel
    {
        public string? UserId { get; set; }
        public string? SiteName { get; set; }
        public List<string> ReferenceNumbers { get; set; } = new List<string>();
    }
}
