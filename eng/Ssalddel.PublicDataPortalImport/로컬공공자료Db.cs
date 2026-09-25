using System.Diagnostics;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using MySqlConnector;
using Ssalddel.Infrastructure.Persistence.PublicData;

internal static class 로컬공공자료Db
{
    public static async Task<DbContextOptions<PublicDataIngestionDbContext>> OptionsAsync(string root)
    {
        var psi=new ProcessStartInfo("docker") { RedirectStandardOutput=true,RedirectStandardError=true,UseShellExecute=false,CreateNoWindow=true };
        psi.ArgumentList.Add("inspect"); psi.ArgumentList.Add("hongdal-mysql-1");
        using var process=Process.Start(psi)!;
        var stdout=process.StandardOutput.ReadToEndAsync(); var stderr=process.StandardError.ReadToEndAsync();
        await process.WaitForExitAsync(); await stderr; Require(process.ExitCode==0,"DockerInspectionFailed");
        using var docker=JsonDocument.Parse(await stdout); var container=docker.RootElement[0];
        var config=container.GetProperty("Config"); var labels=config.GetProperty("Labels");
        var explicitLocalConnection=Environment.GetEnvironmentVariable("SSALDDEL_PUBLIC_DATA_LOCAL_CONNECTION");
        var sharedLocalContainer=!string.IsNullOrWhiteSpace(explicitLocalConnection);
        Require(container.GetProperty("Name").GetString()=="/hongdal-mysql-1" && container.GetProperty("State").GetProperty("Running").GetBoolean(),"ContainerMismatch");
        Require(labels.GetProperty("com.docker.compose.project").GetString()=="hongdal" && labels.GetProperty("com.docker.compose.service").GetString()=="mysql"
            && Path.GetFullPath(labels.GetProperty("com.docker.compose.project.working_dir").GetString()!).TrimEnd('\\','/').Equals(root,StringComparison.OrdinalIgnoreCase),"ComposeMismatch");
        if(!sharedLocalContainer)
            Require(labels.GetProperty("com.docker.compose.project.config_files").GetString()!.EndsWith("docker-compose.dev-deps.yml",StringComparison.Ordinal),"ComposeMismatch");
        Require(container.GetProperty("NetworkSettings").GetProperty("Ports").GetProperty("3306/tcp").EnumerateArray().Any(x=>x.GetProperty("HostPort").GetString()=="13306"),"PortMismatch");
        var env=config.GetProperty("Env").EnumerateArray().Select(x=>x.GetString()!.Split('=',2)).ToDictionary(x=>x[0],x=>x[1]);
        MySqlConnectionStringBuilder cs;
        if(sharedLocalContainer)
        {
            cs=new MySqlConnectionStringBuilder(explicitLocalConnection!);
            Require(cs.Server is "127.0.0.1" or "localhost" && cs.Port==13306 && cs.Database=="hongdal_dev"
                && !string.IsNullOrWhiteSpace(cs.UserID) && cs.UserID!="root" && !string.IsNullOrWhiteSpace(cs.Password),"ExplicitLocalConnectionRejected");
            cs.PersistSecurityInfo=false; cs.Pooling=false; cs.ConnectionTimeout=10; cs.DefaultCommandTimeout=30;
        }
        else
        {
            Require(env["MYSQL_DATABASE"]=="hongdal_dev" && env["MYSQL_USER"]!="root","DatabaseMismatch");
            cs=new MySqlConnectionStringBuilder { Server="127.0.0.1",Port=13306,Database="hongdal_dev",UserID=env["MYSQL_USER"],Password=env["MYSQL_PASSWORD"],PersistSecurityInfo=false,Pooling=false,ConnectionTimeout=10,DefaultCommandTimeout=30 };
        }
        return new DbContextOptionsBuilder<PublicDataIngestionDbContext>().UseMySql(cs.ConnectionString,new MySqlServerVersion(new Version(8,4,0))).Options;
    }
    private static void Require(bool condition,string code){if(!condition)throw new InvalidDataException(code);}
}
