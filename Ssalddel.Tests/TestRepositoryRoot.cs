namespace Ssalddel.Tests;

internal static class TestRepositoryRoot
{
    public static string Find()
    {
        for (var directory = new DirectoryInfo(AppContext.BaseDirectory);
             directory is not null;
             directory = directory.Parent)
        {
            if (File.Exists(Path.Combine(directory.FullName, "Ssalddel.slnx")) &&
                File.Exists(Path.Combine(directory.FullName, "Ssalddel.Tests", "Ssalddel.Tests.csproj")))
            {
                return directory.FullName;
            }
        }

        throw new DirectoryNotFoundException("Ssalddel 시험 저장소 루트를 찾지 못했습니다.");
    }
}
