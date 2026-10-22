using Ssalddel.Contracts.Admin.Progress;

namespace Ssalddel.Application.Admin.Progress;

public sealed record 관리자운송목록조회Query(string? 상태, string? BusinessCode = null) : IRequest<IReadOnlyList<운송진행응답>>;
