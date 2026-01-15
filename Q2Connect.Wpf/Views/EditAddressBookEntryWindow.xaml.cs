using System.Text.RegularExpressions;
using System.Windows;
using Q2Connect.Core.Models;

namespace Q2Connect.Wpf.Views;

public partial class EditAddressBookEntryWindow : Window
{
    private const int MAX_ADDRESS_LENGTH = 512; // Reasonable limit for any address format

    public AddressBookEntry Entry { get; }

    public EditAddressBookEntryWindow(AddressBookEntry entry)
    {
        InitializeComponent();
        Entry = new AddressBookEntry
        {
            Address = entry.Address,
            Label = entry.Label
        };
        DataContext = Entry;
    }

    private void OkButton_Click(object sender, RoutedEventArgs e)
    {
        if (string.IsNullOrWhiteSpace(Entry.Address))
        {
            MessageBox.Show(
                "Address cannot be empty.",
                "Validation Error",
                MessageBoxButton.OK,
                MessageBoxImage.Warning);
            return;
        }

        // Basic validation - allow flexible formats (IP, hostname, FQDN, with/without port)
        // Security is handled by SanitizeAddress() in LauncherService
        var trimmedAddress = Entry.Address.Trim();
        if (!IsValidAddress(trimmedAddress))
        {
            MessageBox.Show(
                "Invalid address format. Address must contain valid characters and be a reasonable length.\n\n" +
                "Accepted formats:\n" +
                "- IP address: 192.168.1.1 or 192.168.1.1:27910\n" +
                "- IPv6 address: [2001:db8::1] or [2001:db8::1]:27910\n" +
                "- Hostname: example.com or example.com:27910\n" +
                "- Local address: localhost or localhost:27910",
                "Invalid Address",
                MessageBoxButton.OK,
                MessageBoxImage.Warning);
            return;
        }

        DialogResult = true;
        Close();
    }

    private static bool IsValidAddress(string address)
    {
        if (string.IsNullOrWhiteSpace(address))
            return false;

        // Reasonable length limit
        if (address.Length > MAX_ADDRESS_LENGTH)
            return false;

        // Allow flexible formats:
        // - IP addresses (IPv4/IPv6) with or without port
        // - Hostnames/FQDNs with or without port
        // - Local addresses
        // Must contain at least some alphanumeric characters
        // Security sanitization happens in LauncherService.SanitizeAddress()
        
        // Check that it's not just special characters
        // Allow: alphanumeric, dots, colons, hyphens, underscores, brackets (for IPv6)
        var hasValidCharacters = Regex.IsMatch(address, @"[a-zA-Z0-9]");
        if (!hasValidCharacters)
            return false;

        // If port is specified, validate it
        var lastColonIndex = address.LastIndexOf(':');
        if (lastColonIndex > 0 && lastColonIndex < address.Length - 1)
        {
            // Check if it's IPv6 format [host]:port or just host:port
            var portPart = address.Substring(lastColonIndex + 1);
            
            // Skip if this is part of IPv6 address (between brackets)
            var bracketBeforeColon = address.LastIndexOf('[');
            var bracketAfterColon = address.LastIndexOf(']');
            var isIpv6Format = bracketBeforeColon >= 0 && bracketAfterColon > bracketBeforeColon && 
                              lastColonIndex > bracketAfterColon;
            
            if (isIpv6Format || bracketBeforeColon < 0)
            {
                // This looks like a port number
                if (int.TryParse(portPart, out var port))
                {
                    if (port < 1 || port > 65535)
                        return false;
                }
                else if (portPart.Length > 0)
                {
                    // Not a valid numeric port
                    return false;
                }
            }
        }

        return true;
    }
}

