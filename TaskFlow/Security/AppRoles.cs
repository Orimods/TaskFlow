namespace TaskFlow.Security;

public static class AppRoles
{
    public const string User = "User";
    public const string Manager = "Manager";
    public const string Admin = "Admin";
    public const string ManagerOrAdmin = Manager + "," + Admin;
}
