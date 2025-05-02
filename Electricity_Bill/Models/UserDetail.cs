using Electricity_Bill.Models;
using Microsoft.AspNetCore.Identity;
using System.ComponentModel.DataAnnotations;

public class UserDetail
{
    [Key]
    public int Id { get; set; }

    public string? UserId { get; set; } 
    public virtual IdentityUser User { get; set; } = new IdentityUser();

    public string? FirstName { get; set; }
    public string? LastName { get; set; }

    public string? ReferenceNumber { get; set; } 
}
