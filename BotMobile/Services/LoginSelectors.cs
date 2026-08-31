namespace BotMobile.Services;

/// <summary>
/// Selector login FB. HASIL PROBE (ProbeRunner), jangan asumsi —
/// kalau login gagal, probe ulang dulu: dotnet run -- --probe
/// </summary>
public static class LoginSelectors
{
    public const string Email = "input[name='email']";
    public const string Pass = "input[name='pass']";
    public const string Submit = "button[name='login']";
    public const string TfaCode = "input[name='approvals_code']";
}
