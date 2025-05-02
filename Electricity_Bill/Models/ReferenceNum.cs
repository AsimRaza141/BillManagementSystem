using Microsoft.EntityFrameworkCore;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Electricity_Bill.Models
{
    public class ReferenceNum
    {
        public int Id { get; set; }

        [Required(ErrorMessage = "Reference Number is required.")]
        public string? ReferenceNumber { get; set; }
        public int SiteId { get; set; }
        public string? UserId { get; set; } // Retained for user association


    }
}
