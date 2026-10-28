namespace BeRdp.Client.Services;

public static class AppRuntime
{
    public static AppConfiguration Configuration => AppConfiguration.Load();
}
