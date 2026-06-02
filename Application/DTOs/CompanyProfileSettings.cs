namespace MGold.Application.DTOs;

public class CompanyProfileSettings
{
    public const string SectionName = "CompanyProfile";

    public string Name { get; set; } = "MGold Kuyumculuk";
    public string Address { get; set; } = "Istanbul / Turkiye";
    public string Phone { get; set; } = "+90 555 000 00 00";
    public string Email { get; set; } = "info@mgold.local";
    public string Website { get; set; } = "www.mgold.local";
    public string TaxOffice { get; set; } = "Merkez";
    public string TaxNumber { get; set; } = "0000000000";
}
