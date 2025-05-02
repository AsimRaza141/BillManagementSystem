using Electricity_Bill.Models;
using System.ComponentModel.DataAnnotations;

public class ElectricityBill
{
    public int Id { get; set; }
    public int Month { get; set; }
    public int Year { get; set; }
    public string? SiteName { get; set; } 
    public string? ReferenceNumber { get; set; }
    public string? UserId { get; set; }
    public double PreviousReading { get; set; }
    public double CurrentReading { get; set; }
    public double NetReading { get; set; }
    public DateTime Date { get; set; }
    public double AmountToPay => CalculateAmount();

    private double CalculateAmount()
    {
        double unitsUsed = CurrentReading - PreviousReading;
        return unitsUsed * 7.85;
    }
}
