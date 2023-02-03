namespace Mariani;

public static class DomainConstraints
{
    public const int MinSiteName = 1;
    public const int MaxSiteName = 128;
    public const int MinUserName = 3;
    public const int MaxUserName = 64;
    public const int MinUserPassword = 4;
    public const int MinTimeZone = -12;
    public const int MaxTimeZone = 12;
}