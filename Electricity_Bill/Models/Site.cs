using System.ComponentModel.DataAnnotations;

namespace Electricity_Bill.Models
{
    public class Site
    {
        public int Id { get; set; }

        [Required(ErrorMessage = "Site Name is required.")]
        public string? SiteName { get; set; }

        public string? UserId { get; set; } 
    }
   
}
