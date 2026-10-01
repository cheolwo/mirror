using Hongdal.Services.Orderer;

namespace Hongdal.Tests.Services.Orderer;

public sealed class 낙관적동시성재시도기Tests
{
    [Fact]
    public async Task 동시에_같은_스냅샷을_변경해도_충돌한_요청을_다시_적용한다()
    {
        var 동기화 = new object();
        var 저장상태 = new 가짜상태(0, []);
        var 최초읽기수 = 0;
        var 두요청읽음 = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);

        async Task<가짜상태> 다시읽기Async(CancellationToken cancellationToken)
        {
            가짜상태 스냅샷;
            var 최초읽기 = false;
            lock (동기화)
            {
                스냅샷 = new 가짜상태(저장상태.버전, [.. 저장상태.값목록]);
                if (스냅샷.버전 == 0)
                {
                    최초읽기 = true;
                    if (++최초읽기수 == 2)
                    {
                        두요청읽음.TrySetResult();
                    }
                }
            }

            if (최초읽기)
            {
                await 두요청읽음.Task.WaitAsync(cancellationToken);
            }

            return 스냅샷;
        }

        Task<bool> 조건부저장Async(
            가짜상태 스냅샷,
            가짜상태 변경결과,
            CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            lock (동기화)
            {
                if (저장상태.버전 != 스냅샷.버전)
                {
                    return Task.FromResult(false);
                }

                저장상태 = 변경결과;
                return Task.FromResult(true);
            }
        }

        Task<가짜상태> 기록Async(string 값)
            => 낙관적동시성재시도기.실행Async(
                다시읽기Async,
                스냅샷 => new 가짜상태(스냅샷.버전 + 1, [.. 스냅샷.값목록, 값]),
                조건부저장Async,
                최대시도횟수: 3,
                CancellationToken.None,
                충돌대기: static (_, _) => Task.CompletedTask);

        await Task.WhenAll(기록Async("첫째"), 기록Async("둘째"));

        Assert.Equal(2, 저장상태.버전);
        Assert.Equal(2, 저장상태.값목록.Count);
        Assert.Contains("첫째", 저장상태.값목록);
        Assert.Contains("둘째", 저장상태.값목록);
    }

    [Fact]
    public async Task 이전_수요_writer가_CAS_재시도해도_이미_저장된_최신_수요를_덮어쓰지_않는다()
    {
        var 동기화 = new object();
        var 저장상태 = new 수요갱신상태(0, null);
        var 최초읽기수 = 0;
        var 이전요청저장시도 = 0;
        var 두요청읽음 = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var 최신요청저장됨 = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var 이전시각 = new DateTime(2026, 7, 17, 1, 0, 0, DateTimeKind.Utc);
        var 최신시각 = 이전시각.AddSeconds(1);

        async Task<수요갱신상태> 다시읽기Async(CancellationToken cancellationToken)
        {
            수요갱신상태 스냅샷;
            var 최초읽기 = false;
            lock (동기화)
            {
                스냅샷 = 저장상태;
                if (스냅샷.버전 == 0)
                {
                    최초읽기 = true;
                    if (++최초읽기수 == 2)
                    {
                        두요청읽음.TrySetResult();
                    }
                }
            }

            if (최초읽기)
            {
                await 두요청읽음.Task.WaitAsync(cancellationToken);
            }

            return 스냅샷;
        }

        Task<수요갱신계획> 기록Async(
            decimal 희망수량,
            DateTime 갱신시각Utc,
            string 갱신토큰,
            bool 이전요청)
            => 낙관적동시성재시도기.실행Async(
                다시읽기Async,
                스냅샷 =>
                {
                    if (스냅샷.수요 is not null
                        && 공동구매자동수요동시성정책.기존수요보존(
                            스냅샷.수요,
                            갱신시각Utc,
                            갱신토큰))
                    {
                        return new 수요갱신계획(false, 스냅샷);
                    }

                    var 수요 = new 공동구매자동수요문서
                    {
                        수요Id = "same-demand",
                        수요출처키 = "same-source",
                        희망수량 = 희망수량,
                        생성시각Utc = 갱신시각Utc,
                        갱신시각Utc = 갱신시각Utc,
                        갱신토큰 = 갱신토큰
                    };
                    return new 수요갱신계획(
                        true,
                        new 수요갱신상태(스냅샷.버전 + 1, 수요));
                },
                async (스냅샷, 계획, cancellationToken) =>
                {
                    if (!계획.변경됨)
                    {
                        return true;
                    }

                    if (이전요청 && Interlocked.Increment(ref 이전요청저장시도) == 1)
                    {
                        await 최신요청저장됨.Task.WaitAsync(cancellationToken);
                    }

                    var 저장됨 = false;
                    lock (동기화)
                    {
                        if (저장상태.버전 == 스냅샷.버전)
                        {
                            저장상태 = 계획.상태;
                            저장됨 = true;
                        }
                    }

                    if (저장됨 && !이전요청)
                    {
                        최신요청저장됨.TrySetResult();
                    }

                    return 저장됨;
                },
                최대시도횟수: 3,
                CancellationToken.None,
                충돌대기: static (_, _) => Task.CompletedTask);

        await Task.WhenAll(
            기록Async(1, 이전시각, "token-a", 이전요청: true),
            기록Async(2, 최신시각, "token-b", 이전요청: false));

        Assert.Equal(1, 저장상태.버전);
        Assert.NotNull(저장상태.수요);
        Assert.Equal(2, 저장상태.수요!.희망수량);
        Assert.Equal(최신시각, 저장상태.수요.갱신시각Utc);
        Assert.Equal("token-b", 저장상태.수요.갱신토큰);
    }

    [Fact]
    public void 서로_다른_집단에_복사본이_있으면_최신_수요가_속한_집단을_선택한다()
    {
        var 이전시각 = new DateTime(2026, 7, 17, 1, 0, 0, DateTimeKind.Utc);
        var 최신시각 = 이전시각.AddSeconds(1);
        var 이전문서 = new 공동구매자동집단문서
        {
            자동집단Id = "group-a",
            수요목록 =
            [
                new 공동구매자동수요문서
                {
                    수요Id = "same-demand",
                    수요출처키 = "same-source",
                    갱신시각Utc = 이전시각,
                    갱신토큰 = "token-a"
                }
            ]
        };
        var 최신문서 = new 공동구매자동집단문서
        {
            자동집단Id = "group-b",
            수요목록 =
            [
                new 공동구매자동수요문서
                {
                    수요Id = "same-demand",
                    수요출처키 = "same-source",
                    갱신시각Utc = 최신시각,
                    갱신토큰 = "token-b"
                }
            ]
        };

        var 최신위치 = 공동구매자동수요동시성정책.최신수요위치(
            [이전문서, 최신문서],
            "same-source");

        Assert.NotNull(최신위치);
        Assert.Same(최신문서, 최신위치!.문서);
        Assert.Equal("token-b", 최신위치.수요.갱신토큰);
    }

    [Fact]
    public async Task 최대시도횟수까지_충돌하면_호출자에게_재시도_가능한_오류를_반환한다()
    {
        var 저장시도수 = 0;

        var 예외 = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            낙관적동시성재시도기.실행Async(
                static _ => Task.FromResult(0),
                static 스냅샷 => 스냅샷 + 1,
                (_, _, _) =>
                {
                    저장시도수++;
                    return Task.FromResult(false);
                },
                최대시도횟수: 2,
                CancellationToken.None,
                충돌대기: static (_, _) => Task.CompletedTask));

        Assert.Equal(2, 저장시도수);
        Assert.Contains("동시 갱신 충돌", 예외.Message, StringComparison.Ordinal);
    }

    private sealed record 가짜상태(int 버전, IReadOnlyList<string> 값목록);

    private sealed record 수요갱신상태(int 버전, 공동구매자동수요문서? 수요);

    private sealed record 수요갱신계획(bool 변경됨, 수요갱신상태 상태);
}
