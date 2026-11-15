using System.Diagnostics;
using System.Text.Json;
using MySqlConnector;

internal sealed class LocalConnectionScope : IDisposable
{
    private const string Variable = "SSALDDEL_PUBLIC_DATA_LOCAL_CONNECTION";
    private readonly string? previous = Environment.GetEnvironmentVariable(Variable);
    public static async Task<LocalConnectionScope> PrepareAsync(string root)
    {
        var scope = new LocalConnectionScope();
        var start = new ProcessStartInfo("docker")
        {
            RedirectStandardOutput = true, RedirectStandardError = true,
            UseShellExecute = false, CreateNoWindow = true,
        };
        start.ArgumentList.Add("inspect"); start.ArgumentList.Add("hongdal-mysql-1");
        using var process = Process.Start(start)!;
        var output = process.StandardOutput.ReadToEndAsync(); var error = process.StandardError.ReadToEndAsync();
        await process.WaitForExitAsync(); await error;
        ImportGuards.Require(process.ExitCode == 0, "ImportDockerInspectionFailed");
        using var data = JsonDocument.Parse(await output);
        var container = data.RootElement[0]; var config = container.GetProperty("Config");
        var labels = config.GetProperty("Labels");
        ImportGuards.Require(container.GetProperty("Name").GetString() == "/hongdal-mysql-1"
            && container.GetProperty("State").GetProperty("Running").GetBoolean()
            && labels.GetProperty("com.docker.compose.project").GetString() == "hongdal"
            && labels.GetProperty("com.docker.compose.service").GetString() == "mysql"
            && Path.TrimEndingDirectorySeparator(Path.GetFullPath(labels.GetProperty("com.docker.compose.project.working_dir").GetString()!))
                .Equals(root, StringComparison.OrdinalIgnoreCase)
            && container.GetProperty("NetworkSettings").GetProperty("Ports").GetProperty("3306/tcp").EnumerateArray()
                .Any(p => p.GetProperty("HostIp").GetString() == "127.0.0.1" && p.GetProperty("HostPort").GetString() == "13306"),
            "ImportDockerTargetRejected");
        if (!string.IsNullOrWhiteSpace(scope.previous)) return scope;
        var env = config.GetProperty("Env").EnumerateArray().Select(x => x.GetString()!.Split('=', 2)).ToDictionary(x => x[0], x => x[1]);
        ImportGuards.Require(env.TryGetValue("MYSQL_USER", out var user) && !string.IsNullOrWhiteSpace(user) && user != "root"
            && env.TryGetValue("MYSQL_PASSWORD", out var password) && !string.IsNullOrWhiteSpace(password), "ImportDockerAccountRejected");
        var connection = new MySqlConnectionStringBuilder
        {
            Server = "127.0.0.1", Port = 13306, Database = "hongdal_dev", UserID = user,
            Password = env["MYSQL_PASSWORD"], PersistSecurityInfo = false, Pooling = false,
            ConnectionTimeout = 10, DefaultCommandTimeout = 30,
        };
        Environment.SetEnvironmentVariable(Variable, connection.ConnectionString);
        return scope;
    }
    public void Dispose() => Environment.SetEnvironmentVariable(Variable, previous);
}
