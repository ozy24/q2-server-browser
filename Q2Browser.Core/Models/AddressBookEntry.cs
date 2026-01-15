namespace Q2Connect.Core.Models;

public class AddressBookEntry
{
    public string Address { get; set; } = string.Empty; // Format: "1.2.3.4:27910"
    public string Label { get; set; } = string.Empty;

    public string DisplayText => string.IsNullOrWhiteSpace(Label) 
        ? Address 
        : $"{Address} [{Label}]";
}

