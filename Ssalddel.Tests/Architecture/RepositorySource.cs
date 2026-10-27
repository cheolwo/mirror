namespace Ssalddel.Tests.Architecture;

/// <summary>소스 구성 시험의 저장소 탐색과 UTF 텍스트 읽기만 담당합니다.</summary>
internal static class RepositorySource
{
    internal static string Read(params string[] path)
        => File.ReadAllText(Path.Combine([FindRoot(), .. path]));

    private static string FindRoot()
    {
        for (var directory = new DirectoryInfo(AppContext.BaseDirectory);
             directory is not null;
             directory = directory.Parent)
        {
            if (File.Exists(Path.Combine(directory.FullName, "Ssalddel.slnx")))
            {
                return directory.FullName;
            }
        }

        throw new DirectoryNotFoundException("Ssalddel 저장소 루트를 찾지 못했습니다.");
    }
}
