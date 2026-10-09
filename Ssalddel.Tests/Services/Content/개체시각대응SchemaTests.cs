using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Ssalddel.Domain.Content;
using Ssalddel.Infrastructure.Persistence.WorldProjection;

namespace Ssalddel.Tests.Services.Content;

/// <summary>모델과 생성 SQL만 검사한다. 실제 서버 연결/DDL 실행은 하지 않는다.</summary>
public sealed class 개체시각대응SchemaTests
{
    [Fact]
    public void 분류주석의고유성외래키와제한삭제를MySql출력까지보존한다()
    {
        using var db = new 개체시각대응DbContext(new DbContextOptionsBuilder<개체시각대응DbContext>()
            .UseMySql("Server=127.0.0.1;Database=not-used;User=test;Password=not-used",
                new MySqlServerVersion(new Version(8, 4, 0))).Options);
        var entity = db.Model.FindEntityType(typeof(보유시각분류주석))!;
        Assert.Equal(new[] { "AnnotationId" }, entity.FindPrimaryKey()!.Properties.Select(x => x.Name));
        var index = Assert.Single(entity.GetIndexes().Where(x => x.IsUnique));
        Assert.Equal(new[] { "SnapshotId", "TaxonomyHash", "TaxonomyPath" }, index.Properties.Select(x => x.Name));
        var foreign = Assert.Single(entity.GetForeignKeys());
        Assert.Equal(DeleteBehavior.Restrict, foreign.DeleteBehavior);
        Assert.Equal(typeof(보유시각자산사본), foreign.PrincipalEntityType.ClrType);
        var generated = db.Database.GenerateCreateScript();
        Assert.Equal(generated, db.Database.GenerateCreateScript());
        Assert.Contains("CREATE UNIQUE INDEX", generated);
        Assert.Contains("ON `world_visual_inventory_classifications` (`SnapshotId`, `TaxonomyHash`, `TaxonomyPath`)", generated);
        Assert.Contains("REFERENCES `world_visual_inventory_snapshots` (`SnapshotId`) ON DELETE RESTRICT", generated);
        var root = TestRepositoryRoot.Find();
        Assert.True(File.Exists(Path.Combine(root, "Ssalddel.Tests/Ssalddel.Tests.csproj")));
        var folder = Path.Combine(root, "artifacts/local/validation/refactor-commit-20260831/development-schema", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(folder);
        var bytes = new UTF8Encoding(false).GetBytes(generated);
        using (var file = new FileStream(Path.Combine(folder, "export.sql"), FileMode.CreateNew)) file.Write(bytes);
        using var record = new FileStream(Path.Combine(folder, "verification.json"), FileMode.CreateNew);
        JsonSerializer.Serialize(record, new { schema = "ModelGenerated_NotApplied", databaseConnected = false,
            ddlExecuted = false, sha256 = Convert.ToHexString(SHA256.HashData(bytes)), uniqueColumns = index.Properties.Select(x => x.Name).ToArray() });
    }
}
