using FluentResults;
using Ssalddel.ApiMetadata;
using Ssalddel.Application.CommandProcessing;
using Microsoft.EntityFrameworkCore;
using Microsoft.AspNetCore.Http;
using Ssalddel.Services.Storage;
using 살뜰.Services.Storage.Local;
using Ssalddel.Application.Driver.Transport;

namespace Ssalddel.Application.Files;

public interface I파일업로드UseCase
{
    Task<Result<파일업로드응답>> 업로드Async(파일업로드Command command, CancellationToken cancellationToken);
}

public sealed record 파일업로드Command(
    IFormFile? File,
    string? CommandName,
    string? ReferenceId);

public sealed record 파일업로드응답(
    string BucketName,
    string ObjectName,
    string Url);

[SsalddelApiWorkflow(SsalddelWorkflow.DomesticTransport)]
[SsalddelUseCase("파일 업로드", Summary = "명령 실행이나 업무 증빙에 필요한 파일을 플랫폼 저장소에 업로드합니다.")]
[SsalddelUseCaseActor(SsalddelActor.Shipper)]
[SsalddelUseCaseActor(SsalddelActor.Driver)]
[SsalddelUseCaseActor(SsalddelActor.PlatformOperator, SsalddelUseCaseActorRole.Supporting)]
public sealed class 파일업로드UseCase : I파일업로드UseCase
{
    private readonly IObjectStorageService _objectStorageService;
    private readonly ICommandFileStoragePathResolver _pathResolver;
    private readonly SsalddelContext _db;
    private readonly ICurrentUserAccessor _currentUserAccessor;

    public 파일업로드UseCase(
        IObjectStorageService objectStorageService,
        ICommandFileStoragePathResolver pathResolver,
        SsalddelContext db,
        ICurrentUserAccessor currentUserAccessor)
    {
        _objectStorageService = objectStorageService;
        _pathResolver = pathResolver;
        _db = db;
        _currentUserAccessor = currentUserAccessor;
    }

    public async Task<Result<파일업로드응답>> 업로드Async(파일업로드Command command, CancellationToken cancellationToken)
    {
        if (command.File is null)
        {
            return Result.Fail<파일업로드응답>("file is required");
        }

        if (command.File.Length <= 0)
        {
            return Result.Fail<파일업로드응답>("empty file is not allowed");
        }

        if (string.IsNullOrWhiteSpace(command.CommandName))
        {
            return Result.Fail<파일업로드응답>("commandName is required");
        }

        var authorizationResult = await ValidateReferenceOwnershipAsync(command, cancellationToken);
        if (authorizationResult.IsFailed)
        {
            return Result.Fail<파일업로드응답>(authorizationResult.Errors.Select(x => x.Message));
        }

        var folder = _pathResolver.ResolveCommandFolder(command.CommandName, command.ReferenceId);
        var photoKind = 화물운송사진증빙Policy.Kind(command.CommandName);
        if (photoKind is not null)
        {
            if (command.File.Length > 화물운송사진증빙Policy.MaximumBytes)
                return Result.Fail<파일업로드응답>("운송 사진은 5MB 이하여야 합니다.");
            var actor = _currentUserAccessor.UserId ?? "";
            var transportId = long.Parse(command.ReferenceId!);
            var transport = await _db.운송원장.SingleOrDefaultAsync(x => x.Id == transportId && x.기사_운송자 == actor, cancellationToken);
            if (transport is null || !화물운송사진증빙Policy.CanUpload(transport, photoKind))
                return Result.Fail<파일업로드응답>("현재 배정된 운송의 상차·하차 단계에서 사진을 촬영해 주세요.");
            var assignmentRound = transport.추천라운드;
            var cargoRequestId = transport.의뢰Id;

            await using var original = command.File.OpenReadStream();
            using var buffer = new MemoryStream();
            await original.CopyToAsync(buffer, cancellationToken);
            var bytes = buffer.ToArray();
            if (!화물운송사진증빙Policy.IsPhoto(bytes))
                return Result.Fail<파일업로드응답>("JPEG 또는 PNG 사진을 다시 촬영·업로드해 주세요.");
            await using var photo = new MemoryStream(bytes, writable: false);
            var uploaded = await _objectStorageService.UploadAsync(photo, command.File.FileName, command.File.ContentType,
                folder, ObjectStorageAccess.Private, cancellationToken);
            // 업로드 중 다른 기사가 배정됐거나 상태가 바뀌면 새 영수증을 현재 업무에 연결하지 않는다.
            await _db.Entry(transport).ReloadAsync(cancellationToken);
            if (transport.기사_운송자 != actor || transport.추천라운드 != assignmentRound || transport.의뢰Id != cargoRequestId
                || !화물운송사진증빙Policy.CanUpload(transport, photoKind))
                return Result.Fail<파일업로드응답>("사진 업로드 중 운송 배정 또는 단계가 변경되었습니다. 현재 운송을 다시 확인해 주세요.");
            화물운송사진증빙Policy.RecordUpload(transport, actor, photoKind, uploaded, bytes);
            await _db.SaveChangesAsync(cancellationToken);
            return Result.Ok(new 파일업로드응답(uploaded.ContainerName, uploaded.ObjectName, uploaded.Url));
        }
        await using var stream = command.File.OpenReadStream();
        var result = await _objectStorageService.UploadAsync(
            stream,
            command.File.FileName,
            command.File.ContentType,
            folder,
            ObjectStorageAccess.Private,
            cancellationToken);

        return Result.Ok(new 파일업로드응답(
            result.ContainerName,
            result.ObjectName,
            result.Url));
    }

    private async Task<Result> ValidateReferenceOwnershipAsync(파일업로드Command command, CancellationToken cancellationToken)
    {
        if (!파일업로드권한정책.운송증빙업로드인가(command.CommandName))
        {
            return Result.Ok();
        }

        if (!long.TryParse(command.ReferenceId, out var transportId) || transportId <= 0)
        {
            return Result.Fail("운송 증빙 업로드에는 유효한 referenceId가 필요합니다.");
        }

        var transport = await _db.운송원장
            .AsNoTracking()
            .FirstOrDefaultAsync(x => x.Id == transportId, cancellationToken);
        if (transport is null)
        {
            return Result.Fail("참조 운송을 찾을 수 없습니다.");
        }

        return 파일업로드권한정책.운송증빙업로드권한있음(transport, _currentUserAccessor.UserId, _currentUserAccessor.Role)
            ? Result.Ok()
            : Result.Fail("파일 업로드 권한이 없습니다.");
    }
}
