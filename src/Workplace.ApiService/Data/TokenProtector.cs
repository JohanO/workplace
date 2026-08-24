using Microsoft.AspNetCore.DataProtection;

namespace Workplace.ApiService.Data;

public class TokenProtector(IDataProtectionProvider provider)
{
    private const string Purpose = "Workplace.ConnectedAccount.Tokens.v1";

    private readonly IDataProtector _protector = provider.CreateProtector(Purpose);

    public string Protect(string plaintext) => _protector.Protect(plaintext);

    public string Unprotect(string protectedText) => _protector.Unprotect(protectedText);
}
