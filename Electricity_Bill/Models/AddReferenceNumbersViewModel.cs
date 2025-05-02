namespace Electricity_Bill.Models
{
    public class AddReferenceNumbersViewModel
    {
        public string? UserId { get; set; }
        public List<string> ReferenceNumbers { get; set; } = new List<string>();
    }
}
