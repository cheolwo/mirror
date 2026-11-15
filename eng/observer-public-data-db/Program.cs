using System.Text.Json;

var result = new Dictionary<string, object?>
{
    ["schemaVersion"] = "observer-public-data-db-receipt.v1",
    ["databaseTarget"] = "hongdal-mysql-1 / 127.0.0.1:13306 / hongdal_dev",
    ["databaseWriteAttempted"] = false, ["committed"] = false,
    ["collectionExecuted"] = false, ["migrationExecuted"] = false,
    ["packPublished"] = false, ["gameApplied"] = false,
};
string? receipt = null;
var handles = new List<FileStream>();
try
{
    if (args.Length == 1 && args[0] == "self-test")
    {
        result["selfTestsPassed"] = SelfTests.Run();
        result["status"] = "self_test_passed";
    }
    else
    {
        var input = ImportGuards.ParseArguments(args);
        receipt = input.Receipt;
        var manifest = ImportGuards.Load(input, handles);
        result["mode"] = input.Mode; result["manifestSha256"] = input.Sha256;
        result["entryCount"] = manifest.Count;
        using var scope = await LocalConnectionScope.PrepareAsync(input.Root);
        var options = await 로컬공공자료Db.OptionsAsync(input.Root);
        await ImportDatabase.RunAsync(input, manifest, options, result);
        result["status"] = "completed";
    }
}
catch (Exception error)
{
    result["status"] = "failed";
    result["errorCode"] = error is ImportGuardException guarded ? guarded.Code : error.GetType().Name;
}
finally
{
    foreach (var handle in handles) handle.Dispose();
}
try
{
    var bytes = JsonSerializer.SerializeToUtf8Bytes(result, ImportGuards.Json);
    if (receipt is not null)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(receipt)!);
        using var stream = new FileStream(receipt, FileMode.CreateNew, FileAccess.Write, FileShare.None);
        stream.Write(bytes); stream.Flush(true);
    }
    Console.WriteLine(System.Text.Encoding.UTF8.GetString(bytes));
}
catch (Exception error)
{
    result["receiptErrorCode"] = error.GetType().Name;
    result["status"] = "failed";
    Console.WriteLine(JsonSerializer.Serialize(result, ImportGuards.Json));
}
return Equals(result.GetValueOrDefault("status"), "failed") ? 1 : 0;
