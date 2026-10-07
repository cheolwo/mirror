namespace Ssalddel.Ui.Common.Areas.App.Models;

/// <summary>지도에서 읽던 목록의 페이지와 구분만 보관하며 업무 원문은 포함하지 않습니다.</summary>
public sealed record NeighborhoodMapListContext(string Kind, int Page, string? Scope = null);
