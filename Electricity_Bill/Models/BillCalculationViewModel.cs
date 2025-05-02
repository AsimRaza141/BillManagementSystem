namespace Electricity_Bill.Models
{
    public class BillCalculationViewModel
    {
        public List<ElectricityBill> Bills { get; set; } = new List<ElectricityBill>();
        public DateTime StartDate { get; set; }
        public DateTime EndDate { get; set; }
        public int SelectedSiteId { get; set; } 
        public string? SelectedReferenceNumber { get; set; }
        public string? SiteName { get; set; } 
        public string? ReferenceNumber { get; set; } 
        public double PreviousReading { get; set; }
        public double CurrentReading { get; set; }
        public double NetReading { get; set; }
        public int Year { get; set; }
        public int Month { get; set; }
        public int Id { get; set; }
        public List<Site> Sites { get; set; } = new List<Site>(); 
        public List<string> ReferenceNumbers { get; set; } = new List<string>(); 
    }

}
