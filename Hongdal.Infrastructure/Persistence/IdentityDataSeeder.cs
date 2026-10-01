using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using 홍달.도메인.공통;
using 홍달.도메인.기사;
using 홍달.도메인.차량;
using 홍달.도메인.사용자;
using 홍달.도메인.운송;

namespace 홍달.Data
{
    public static class IdentityDataSeeder
    {
        public static async Task SeedAsync(IServiceProvider serviceProvider)
        {
            using var scope = serviceProvider.CreateScope();
            var roleManager = scope.ServiceProvider.GetRequiredService<RoleManager<IdentityRole>>();
            var userManager = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();
            var db = scope.ServiceProvider.GetRequiredService<HongdalContext>();

            var roles = new[]
            {
                역할명.기사,
                역할명.화주,
                역할명.판매자,
                역할명.창고관리자,
                역할명.보세창고운영자,
                역할명.FTZ운영자,
                역할명.보세운송사,
                역할명.풀필먼트운영자,
                역할명.택배운송사,
                역할명.관세사,
                역할명.서버관리자
            };
            foreach (var role in roles)
            {
                if (!await roleManager.RoleExistsAsync(role))
                {
                    await roleManager.CreateAsync(new IdentityRole(role));
                }
            }

            var adminId = "qkrcjfdn79";
            var adminEmail = "qkrcjfdn79@hongdal.local";
            var adminUser = await userManager.FindByNameAsync(adminId);
            if (adminUser == null)
            {
                adminUser = new ApplicationUser
                {
                    UserName = adminId,
                    Email = adminEmail,
                    EmailConfirmed = true,
                };

                var createResult = await userManager.CreateAsync(adminUser, "tlstjstns79!");
                if (!createResult.Succeeded)
                {
                    return;
                }
            }

            if (!await userManager.IsInRoleAsync(adminUser, 역할명.서버관리자))
            {
                await userManager.AddToRoleAsync(adminUser, 역할명.서버관리자);
            }

            var legacyAdminEmail = "admin@hongdal.local";
            var legacyAdminUser = await userManager.FindByEmailAsync(legacyAdminEmail);
            if (legacyAdminUser != null && !string.Equals(legacyAdminUser.UserName, adminId, StringComparison.OrdinalIgnoreCase))
            {
                if (await userManager.IsInRoleAsync(legacyAdminUser, 역할명.서버관리자))
                {
                    await userManager.RemoveFromRoleAsync(legacyAdminUser, 역할명.서버관리자);
                }

                await userManager.DeleteAsync(legacyAdminUser);
            }

            var driverEmail = "driver1@hongdal.local";
            var driverUser = await userManager.FindByEmailAsync(driverEmail);
            if (driverUser == null)
            {
                driverUser = new ApplicationUser
                {
                    UserName = driverEmail,
                    Email = driverEmail,
                    EmailConfirmed = true,
                };

                var createResult = await userManager.CreateAsync(driverUser, "Driver123!");
                if (!createResult.Succeeded)
                {
                    return;
                }
            }

            if (!await userManager.IsInRoleAsync(driverUser, 역할명.기사))
            {
                await userManager.AddToRoleAsync(driverUser, 역할명.기사);
            }

            var shipperId = "shipper1";
            var shipperEmail = "shipper1@hongdal.local";
            var shipperUser = await userManager.FindByNameAsync(shipperId) ?? await userManager.FindByEmailAsync(shipperEmail);
            if (shipperUser == null)
            {
                shipperUser = new ApplicationUser
                {
                    UserName = shipperId,
                    Email = shipperEmail,
                    EmailConfirmed = true,
                    BusinessRegistrationNumber = "123-45-67890"
                };

                var createResult = await userManager.CreateAsync(shipperUser, "Shipper123!");
                if (!createResult.Succeeded)
                {
                    return;
                }
            }
            else
            {
                var needsUpdate = false;
                if (!string.Equals(shipperUser.UserName, shipperId, StringComparison.OrdinalIgnoreCase))
                {
                    shipperUser.UserName = shipperId;
                    needsUpdate = true;
                }

                if (!string.Equals(shipperUser.Email, shipperEmail, StringComparison.OrdinalIgnoreCase))
                {
                    shipperUser.Email = shipperEmail;
                    shipperUser.EmailConfirmed = true;
                    needsUpdate = true;
                }

                if (!string.Equals(shipperUser.BusinessRegistrationNumber, "123-45-67890", StringComparison.OrdinalIgnoreCase))
                {
                    shipperUser.BusinessRegistrationNumber = "123-45-67890";
                    needsUpdate = true;
                }

                if (needsUpdate)
                {
                    await userManager.UpdateAsync(shipperUser);
                }
            }

            if (!await userManager.IsInRoleAsync(shipperUser, 역할명.화주))
            {
                await userManager.AddToRoleAsync(shipperUser, 역할명.화주);
            }

            if (!await userManager.IsInRoleAsync(shipperUser, 역할명.판매자))
            {
                await userManager.AddToRoleAsync(shipperUser, 역할명.판매자);
            }

            if (!await userManager.IsInRoleAsync(shipperUser, 역할명.창고관리자))
            {
                await userManager.AddToRoleAsync(shipperUser, 역할명.창고관리자);
            }

            await EnsureOrdererProfileAsync(db, adminUser, "관리자 주문자", "010-0000-0001");
            await EnsureOrdererProfileAsync(db, driverUser, "개발용 기사 주문자", "010-0000-0000");
            await EnsureOrdererProfileAsync(db, shipperUser, "개발용 화주 주문자", "010-0000-0002");

            var driverProfile = await db.용달기사.FirstOrDefaultAsync(d => d.기사Id == driverUser.Id);
            if (driverProfile == null)
            {
                db.용달기사.Add(new 용달기사
                {
                    기사명 = "개발용 기사",
                    기사Id = driverUser.Id,
                    상태 = "활동중",
                    연락처 = "010-0000-0000",
                    차량 = "오토바이",
                    운행상태 = 상태값.기사운행상태.대기,
                    주_활동지역 = "서울",
                    메모 = "개발 시드 데이터",
                    등록일 = DateTime.UtcNow,
                    CreatedAt = DateTime.UtcNow,
                    UpdatedAt = DateTime.UtcNow
                });
            }

            var 차량단가시드 = new[]
            {
                new { 차량종류 = "오토바이", 기본운임 = 5000m, Km당단가 = 1000m, 최소운임 = 5000m, 야간할증 = 1000m, 우천할증 = 500m },
                new { 차량종류 = "다마스", 기본운임 = 15000m, Km당단가 = 1200m, 최소운임 = 15000m, 야간할증 = 0m, 우천할증 = 0m },
                new { 차량종류 = "라보", 기본운임 = 20000m, Km당단가 = 1300m, 최소운임 = 20000m, 야간할증 = 0m, 우천할증 = 0m },
                new { 차량종류 = "1톤 카고", 기본운임 = 35000m, Km당단가 = 1300m, 최소운임 = 35000m, 야간할증 = 0m, 우천할증 = 0m },
                new { 차량종류 = "1톤 탑차", 기본운임 = 35000m, Km당단가 = 1350m, 최소운임 = 35000m, 야간할증 = 0m, 우천할증 = 0m },
                new { 차량종류 = "1톤 윙바디", 기본운임 = 35000m, Km당단가 = 1400m, 최소운임 = 35000m, 야간할증 = 0m, 우천할증 = 0m },
                new { 차량종류 = "1톤 냉장탑", 기본운임 = 35000m, Km당단가 = 1450m, 최소운임 = 35000m, 야간할증 = 0m, 우천할증 = 0m },
                new { 차량종류 = "1톤 냉동탑", 기본운임 = 35000m, Km당단가 = 1500m, 최소운임 = 35000m, 야간할증 = 0m, 우천할증 = 0m },
                new { 차량종류 = "1.4톤 카고", 기본운임 = 45000m, Km당단가 = 1550m, 최소운임 = 45000m, 야간할증 = 0m, 우천할증 = 0m },
                new { 차량종류 = "1.4톤 탑차", 기본운임 = 45000m, Km당단가 = 1600m, 최소운임 = 45000m, 야간할증 = 0m, 우천할증 = 0m },
                new { 차량종류 = "1.4톤 윙바디", 기본운임 = 45000m, Km당단가 = 1650m, 최소운임 = 45000m, 야간할증 = 0m, 우천할증 = 0m },
                new { 차량종류 = "2.5톤 카고", 기본운임 = 60000m, Km당단가 = 1700m, 최소운임 = 60000m, 야간할증 = 0m, 우천할증 = 0m },
                new { 차량종류 = "2.5톤 탑차", 기본운임 = 60000m, Km당단가 = 1750m, 최소운임 = 60000m, 야간할증 = 0m, 우천할증 = 0m },
                new { 차량종류 = "2.5톤 윙바디", 기본운임 = 60000m, Km당단가 = 1800m, 최소운임 = 60000m, 야간할증 = 0m, 우천할증 = 0m },
                new { 차량종류 = "3.5톤 카고", 기본운임 = 80000m, Km당단가 = 1850m, 최소운임 = 80000m, 야간할증 = 0m, 우천할증 = 0m },
                new { 차량종류 = "5톤 카고", 기본운임 = 100000m, Km당단가 = 1900m, 최소운임 = 100000m, 야간할증 = 0m, 우천할증 = 0m },
                new { 차량종류 = "5톤 탑차", 기본운임 = 100000m, Km당단가 = 1950m, 최소운임 = 100000m, 야간할증 = 0m, 우천할증 = 0m },
                new { 차량종류 = "5톤 윙바디", 기본운임 = 100000m, Km당단가 = 2000m, 최소운임 = 100000m, 야간할증 = 0m, 우천할증 = 0m }
            };

            var 차량제원시드 = new[]
            {
                new 차량제원
                {
                    차량코드 = "1톤 카고",
                    차량명 = "1톤 카고",
                    제조사 = "Kia",
                    모델명 = "봉고3",
                    차급 = "1톤",
                    차체형태 = "카고",
                    적재함길이Mm = 3110,
                    적재함폭Mm = 1630,
                    적재함높이Mm = 355,
                    최대적재중량Kg = 1000,
                    운영권장중량Kg = 850,
                    차량전체높이Mm = 1990,
                    바닥높이Mm = 840,
                    비눈보호가능 = false,
                    냉장가능 = false,
                    냉동가능 = false,
                    측면상하차가능 = false,
                    리프트가능 = false,
                    장재물유리 = true,
                    팔레트적재개수 = 1,
                    기준연비KmPerLiter = 8m,
                    장점메모 = "범용성이 높음",
                    단점메모 = "비·눈 보호가 약함"
                },
                new 차량제원
                {
                    차량코드 = "1톤 카고-표준캡",
                    차량명 = "1톤 카고 표준캡",
                    제조사 = "Kia",
                    모델명 = "봉고3",
                    차급 = "1톤",
                    차체형태 = "카고",
                    적재함길이Mm = 3110,
                    적재함폭Mm = 1630,
                    적재함높이Mm = 355,
                    최대적재중량Kg = 1000,
                    운영권장중량Kg = 850,
                    차량전체높이Mm = 1990,
                    바닥높이Mm = 840,
                    비눈보호가능 = false,
                    냉장가능 = false,
                    냉동가능 = false,
                    측면상하차가능 = false,
                    리프트가능 = false,
                    장재물유리 = true,
                    팔레트적재개수 = 1,
                    기준연비KmPerLiter = 8m,
                    장점메모 = "봉고3 표준캡 기준",
                    단점메모 = "실내공간보다 적재공간 중심"
                },
                new 차량제원
                {
                    차량코드 = "1톤 카고-킹캡",
                    차량명 = "1톤 카고 킹캡",
                    제조사 = "Kia",
                    모델명 = "봉고3",
                    차급 = "1톤",
                    차체형태 = "카고",
                    적재함길이Mm = 2860,
                    적재함폭Mm = 1630,
                    적재함높이Mm = 355,
                    최대적재중량Kg = 1000,
                    운영권장중량Kg = 850,
                    차량전체높이Mm = 1990,
                    바닥높이Mm = 840,
                    비눈보호가능 = false,
                    냉장가능 = false,
                    냉동가능 = false,
                    측면상하차가능 = false,
                    리프트가능 = false,
                    장재물유리 = true,
                    팔레트적재개수 = 1,
                    기준연비KmPerLiter = 8m,
                    장점메모 = "봉고3 킹캡 기준",
                    단점메모 = "표준캡보다 길이 여유가 짧음"
                },
                new 차량제원
                {
                    차량코드 = "1톤 카고-더블캡",
                    차량명 = "1톤 카고 더블캡",
                    제조사 = "Kia",
                    모델명 = "봉고3",
                    차급 = "1톤",
                    차체형태 = "카고",
                    적재함길이Mm = 2185,
                    적재함폭Mm = 1630,
                    적재함높이Mm = 355,
                    최대적재중량Kg = 1000,
                    운영권장중량Kg = 800,
                    차량전체높이Mm = 1990,
                    바닥높이Mm = 840,
                    비눈보호가능 = false,
                    냉장가능 = false,
                    냉동가능 = false,
                    측면상하차가능 = false,
                    리프트가능 = false,
                    장재물유리 = false,
                    팔레트적재개수 = 1,
                    기준연비KmPerLiter = 7.8m,
                    장점메모 = "승차공간이 넓음",
                    단점메모 = "적재장 길이가 크게 짧음"
                },
                new 차량제원
                {
                    차량코드 = "1톤 카고-포터2",
                    차량명 = "1톤 카고 포터2",
                    제조사 = "Hyundai",
                    모델명 = "포터2",
                    차급 = "1톤",
                    차체형태 = "카고",
                    적재함길이Mm = 3120,
                    적재함폭Mm = 1620,
                    적재함높이Mm = 355,
                    최대적재중량Kg = 1000,
                    운영권장중량Kg = 850,
                    차량전체높이Mm = 1970,
                    바닥높이Mm = 830,
                    비눈보호가능 = false,
                    냉장가능 = false,
                    냉동가능 = false,
                    측면상하차가능 = false,
                    리프트가능 = false,
                    장재물유리 = true,
                    팔레트적재개수 = 1,
                    기준연비KmPerLiter = 8.2m,
                    장점메모 = "포터2 표준 카고 기준",
                    단점메모 = "비·눈 보호가 필요하면 탑차가 더 적합"
                },
                new 차량제원
                {
                    차량코드 = "1톤 탑차",
                    차량명 = "1톤 탑차",
                    제조사 = "Kia",
                    모델명 = "봉고3",
                    차급 = "1톤",
                    차체형태 = "내장탑",
                    적재함길이Mm = 2830,
                    적재함폭Mm = 1670,
                    적재함높이Mm = 1160,
                    최대적재중량Kg = 1000,
                    운영권장중량Kg = 900,
                    비눈보호가능 = true,
                    냉장가능 = false,
                    냉동가능 = false,
                    측면상하차가능 = false,
                    리프트가능 = false,
                    장재물유리 = false,
                    팔레트적재개수 = 1,
                    기준연비KmPerLiter = 7m,
                    장점메모 = "비·눈 보호 가능",
                    단점메모 = "카고보다 적재 유연성이 낮음"
                },
                new 차량제원
                {
                    차량코드 = "1톤 탑차-표준",
                    차량명 = "1톤 탑차 표준",
                    제조사 = "Kia",
                    모델명 = "봉고3",
                    차급 = "1톤",
                    차체형태 = "내장탑",
                    적재함길이Mm = 2830,
                    적재함폭Mm = 1670,
                    적재함높이Mm = 1580,
                    최대적재중량Kg = 1000,
                    운영권장중량Kg = 900,
                    비눈보호가능 = true,
                    냉장가능 = false,
                    냉동가능 = false,
                    측면상하차가능 = false,
                    리프트가능 = false,
                    장재물유리 = false,
                    팔레트적재개수 = 1,
                    기준연비KmPerLiter = 7m,
                    장점메모 = "표준 높이의 탑차",
                    단점메모 = "높이 제한 확인 필요"
                },
                new 차량제원
                {
                    차량코드 = "1톤 탑차-하이",
                    차량명 = "1톤 탑차 하이",
                    제조사 = "Kia",
                    모델명 = "봉고3",
                    차급 = "1톤",
                    차체형태 = "내장탑",
                    적재함길이Mm = 2830,
                    적재함폭Mm = 1670,
                    적재함높이Mm = 1810,
                    최대적재중량Kg = 1000,
                    운영권장중량Kg = 880,
                    비눈보호가능 = true,
                    냉장가능 = false,
                    냉동가능 = false,
                    측면상하차가능 = false,
                    리프트가능 = false,
                    장재물유리 = false,
                    팔레트적재개수 = 1,
                    기준연비KmPerLiter = 7m,
                    장점메모 = "높이 여유가 큰 탑차",
                    단점메모 = "상부 적재 제약 확인 필요"
                },
                new 차량제원
                {
                    차량코드 = "1톤 냉동탑",
                    차량명 = "1톤 냉동탑",
                    제조사 = "Kia",
                    모델명 = "봉고3",
                    차급 = "1톤",
                    차체형태 = "냉동탑",
                    적재함길이Mm = 2740,
                    적재함폭Mm = 1610,
                    적재함높이Mm = 1070,
                    최대적재중량Kg = 1000,
                    운영권장중량Kg = 850,
                    비눈보호가능 = true,
                    냉장가능 = true,
                    냉동가능 = true,
                    측면상하차가능 = false,
                    리프트가능 = false,
                    장재물유리 = false,
                    팔레트적재개수 = 1,
                    기준연비KmPerLiter = 5.5m,
                    장점메모 = "냉장/냉동 운송에 적합",
                    단점메모 = "유지비와 제약이 큼"
                },
                new 차량제원
                {
                    차량코드 = "1톤 냉동탑-표준",
                    차량명 = "1톤 냉동탑 표준",
                    제조사 = "Kia",
                    모델명 = "봉고3",
                    차급 = "1톤",
                    차체형태 = "냉동탑",
                    적재함길이Mm = 2740,
                    적재함폭Mm = 1610,
                    적재함높이Mm = 1070,
                    최대적재중량Kg = 1000,
                    운영권장중량Kg = 850,
                    비눈보호가능 = true,
                    냉장가능 = true,
                    냉동가능 = true,
                    측면상하차가능 = false,
                    리프트가능 = false,
                    장재물유리 = false,
                    팔레트적재개수 = 1,
                    기준연비KmPerLiter = 5.5m,
                    장점메모 = "냉장/냉동 운송 표준형",
                    단점메모 = "적재함 높이가 낮은 편"
                },
                new 차량제원
                {
                    차량코드 = "1.2톤 카고",
                    차량명 = "1.2톤 카고",
                    제조사 = "Kia",
                    모델명 = "봉고3",
                    차급 = "1.2톤",
                    차체형태 = "카고",
                    적재함길이Mm = 3400,
                    적재함폭Mm = 1650,
                    적재함높이Mm = 355,
                    최대적재중량Kg = 1200,
                    운영권장중량Kg = 1000,
                    차량전체높이Mm = 1995,
                    바닥높이Mm = 840,
                    비눈보호가능 = false,
                    냉장가능 = false,
                    냉동가능 = false,
                    측면상하차가능 = false,
                    리프트가능 = false,
                    장재물유리 = true,
                    팔레트적재개수 = 2,
                    기준연비KmPerLiter = 7.5m,
                    장점메모 = "길이와 중량 여유가 큼",
                    단점메모 = "과적 리스크 관리 필요"
                },
                new 차량제원
                {
                    차량코드 = "1.4톤 카고",
                    차량명 = "1.4톤 카고",
                    제조사 = "Kia",
                    모델명 = "봉고3",
                    차급 = "1.4톤",
                    차체형태 = "카고",
                    적재함길이Mm = 3400,
                    적재함폭Mm = 1650,
                    적재함높이Mm = 355,
                    최대적재중량Kg = 1400,
                    운영권장중량Kg = 1200,
                    차량전체높이Mm = 1995,
                    바닥높이Mm = 840,
                    비눈보호가능 = false,
                    냉장가능 = false,
                    냉동가능 = false,
                    측면상하차가능 = false,
                    리프트가능 = false,
                    장재물유리 = true,
                    팔레트적재개수 = 2,
                    기준연비KmPerLiter = 7.5m,
                    장점메모 = "길이 여유가 큼",
                    단점메모 = "과적 관리가 필요함"
                },
                new 차량제원
                {
                    차량코드 = "2.5톤 카고",
                    차량명 = "2.5톤 카고",
                    제조사 = "Kia",
                    모델명 = "마이티",
                    차급 = "2.5톤",
                    차체형태 = "카고",
                    적재함길이Mm = 4800,
                    적재함폭Mm = 1950,
                    적재함높이Mm = 400,
                    최대적재중량Kg = 2500,
                    운영권장중량Kg = 2100,
                    차량전체높이Mm = 2300,
                    바닥높이Mm = 1100,
                    비눈보호가능 = false,
                    냉장가능 = false,
                    냉동가능 = false,
                    측면상하차가능 = false,
                    리프트가능 = false,
                    장재물유리 = true,
                    팔레트적재개수 = 3,
                    기준연비KmPerLiter = 5.8m,
                    장점메모 = "중량/부피 균형이 좋음",
                    단점메모 = "도심 회전과 주차 제약"
                },
                new 차량제원
                {
                    차량코드 = "2.5톤 탑차",
                    차량명 = "2.5톤 탑차",
                    제조사 = "Kia",
                    모델명 = "마이티",
                    차급 = "2.5톤",
                    차체형태 = "내장탑",
                    적재함길이Mm = 4700,
                    적재함폭Mm = 1900,
                    적재함높이Mm = 2200,
                    최대적재중량Kg = 2500,
                    운영권장중량Kg = 2000,
                    비눈보호가능 = true,
                    냉장가능 = false,
                    냉동가능 = false,
                    측면상하차가능 = false,
                    리프트가능 = false,
                    장재물유리 = false,
                    팔레트적재개수 = 3,
                    기준연비KmPerLiter = 5.2m,
                    장점메모 = "비노출 화물에 유리",
                    단점메모 = "높이와 무게를 같이 봐야 함"
                },
                new 차량제원
                {
                    차량코드 = "2.5톤 윙바디",
                    차량명 = "2.5톤 윙바디",
                    제조사 = "Kia",
                    모델명 = "마이티",
                    차급 = "2.5톤",
                    차체형태 = "윙바디",
                    적재함길이Mm = 4700,
                    적재함폭Mm = 2000,
                    적재함높이Mm = 2200,
                    최대적재중량Kg = 2500,
                    운영권장중량Kg = 2000,
                    비눈보호가능 = true,
                    냉장가능 = false,
                    냉동가능 = false,
                    측면상하차가능 = true,
                    리프트가능 = true,
                    장재물유리 = false,
                    팔레트적재개수 = 3,
                    기준연비KmPerLiter = 5.1m,
                    장점메모 = "팔레트/측면상하차에 유리",
                    단점메모 = "윙 개폐 공간이 필요"
                },
                new 차량제원
                {
                    차량코드 = "3.5톤 카고",
                    차량명 = "3.5톤 카고",
                    제조사 = "Hyundai",
                    모델명 = "메가트럭",
                    차급 = "3.5톤",
                    차체형태 = "카고",
                    적재함길이Mm = 5600,
                    적재함폭Mm = 2100,
                    적재함높이Mm = 400,
                    최대적재중량Kg = 3500,
                    운영권장중량Kg = 3000,
                    차량전체높이Mm = 2450,
                    바닥높이Mm = 1150,
                    비눈보호가능 = false,
                    냉장가능 = false,
                    냉동가능 = false,
                    측면상하차가능 = false,
                    리프트가능 = false,
                    장재물유리 = true,
                    팔레트적재개수 = 4,
                    기준연비KmPerLiter = 4.8m,
                    장점메모 = "장거리/중량 운송에 적합",
                    단점메모 = "도심 제약이 큼"
                },
                new 차량제원
                {
                    차량코드 = "3.5톤 탑차",
                    차량명 = "3.5톤 탑차",
                    제조사 = "Hyundai",
                    모델명 = "메가트럭",
                    차급 = "3.5톤",
                    차체형태 = "내장탑",
                    적재함길이Mm = 5500,
                    적재함폭Mm = 2100,
                    적재함높이Mm = 2300,
                    최대적재중량Kg = 3500,
                    운영권장중량Kg = 2800,
                    비눈보호가능 = true,
                    냉장가능 = false,
                    냉동가능 = false,
                    측면상하차가능 = false,
                    리프트가능 = false,
                    장재물유리 = false,
                    팔레트적재개수 = 4,
                    기준연비KmPerLiter = 4.5m,
                    장점메모 = "대형 박스/가전 운송에 적합",
                    단점메모 = "높이 제한과 적재 순서 확인 필요"
                },
                new 차량제원
                {
                    차량코드 = "3.5톤 윙바디",
                    차량명 = "3.5톤 윙바디",
                    제조사 = "Hyundai",
                    모델명 = "메가트럭",
                    차급 = "3.5톤",
                    차체형태 = "윙바디",
                    적재함길이Mm = 5500,
                    적재함폭Mm = 2200,
                    적재함높이Mm = 2300,
                    최대적재중량Kg = 3500,
                    운영권장중량Kg = 2800,
                    비눈보호가능 = true,
                    냉장가능 = false,
                    냉동가능 = false,
                    측면상하차가능 = true,
                    리프트가능 = true,
                    장재물유리 = false,
                    팔레트적재개수 = 4,
                    기준연비KmPerLiter = 4.4m,
                    장점메모 = "팔레트 작업이 편함",
                    단점메모 = "윙 개방 공간 확보 필요"
                },
                new 차량제원
                {
                    차량코드 = "5톤 카고",
                    차량명 = "5톤 카고",
                    제조사 = "Hyundai",
                    모델명 = "메가트럭",
                    차급 = "5톤",
                    차체형태 = "카고",
                    적재함길이Mm = 6200,
                    적재함폭Mm = 2200,
                    적재함높이Mm = 400,
                    최대적재중량Kg = 5000,
                    운영권장중량Kg = 4200,
                    차량전체높이Mm = 2500,
                    바닥높이Mm = 1200,
                    비눈보호가능 = false,
                    냉장가능 = false,
                    냉동가능 = false,
                    측면상하차가능 = false,
                    리프트가능 = false,
                    장재물유리 = true,
                    팔레트적재개수 = 5,
                    기준연비KmPerLiter = 4.2m,
                    장점메모 = "대형 중량 화물 적합",
                    단점메모 = "도로/회차 제약이 큼"
                },
                new 차량제원
                {
                    차량코드 = "5톤 탑차",
                    차량명 = "5톤 탑차",
                    제조사 = "Hyundai",
                    모델명 = "메가트럭",
                    차급 = "5톤",
                    차체형태 = "내장탑",
                    적재함길이Mm = 6100,
                    적재함폭Mm = 2200,
                    적재함높이Mm = 2400,
                    최대적재중량Kg = 5000,
                    운영권장중량Kg = 4000,
                    비눈보호가능 = true,
                    냉장가능 = false,
                    냉동가능 = false,
                    측면상하차가능 = false,
                    리프트가능 = false,
                    장재물유리 = false,
                    팔레트적재개수 = 5,
                    기준연비KmPerLiter = 4m,
                    장점메모 = "대형 상온 화물에 적합",
                    단점메모 = "높이/폭 제한 확인 필요"
                },
                new 차량제원
                {
                    차량코드 = "5톤 윙바디",
                    차량명 = "5톤 윙바디",
                    제조사 = "Hyundai",
                    모델명 = "메가트럭",
                    차급 = "5톤",
                    차체형태 = "윙바디",
                    적재함길이Mm = 6100,
                    적재함폭Mm = 2300,
                    적재함높이Mm = 2400,
                    최대적재중량Kg = 5000,
                    운영권장중량Kg = 4000,
                    비눈보호가능 = true,
                    냉장가능 = false,
                    냉동가능 = false,
                    측면상하차가능 = true,
                    리프트가능 = true,
                    장재물유리 = false,
                    팔레트적재개수 = 5,
                    기준연비KmPerLiter = 3.9m,
                    장점메모 = "대형 팔레트/적재 편의성",
                    단점메모 = "운행/상하차 공간이 많이 필요"
                },
                new 차량제원
                {
                    차량코드 = "1톤 윙바디",
                    차량명 = "1톤 윙바디",
                    제조사 = "Kia",
                    모델명 = "봉고3",
                    차급 = "1톤",
                    차체형태 = "윙바디",
                    적재함길이Mm = 2820,
                    적재함폭Mm = 1700,
                    적재함높이Mm = 1800,
                    최대적재중량Kg = 1000,
                    운영권장중량Kg = 850,
                    비눈보호가능 = true,
                    냉장가능 = false,
                    냉동가능 = false,
                    측면상하차가능 = true,
                    리프트가능 = true,
                    장재물유리 = false,
                    팔레트적재개수 = 2,
                    기준연비KmPerLiter = 6.8m,
                    장점메모 = "측면 상하차와 팔레트 작업에 유리",
                    단점메모 = "높이와 폭 제약 확인 필요"
                }
            };

            var 기존차량종류 = await db.차량단가
                .Select(x => x.차량종류)
                .ToListAsync();

            var 기존차량종류셋 = new HashSet<string>(기존차량종류, StringComparer.OrdinalIgnoreCase);

            foreach (var row in 차량단가시드)
            {
                if (기존차량종류셋.Contains(row.차량종류))
                {
                    continue;
                }

                db.차량단가.Add(new 차량단가
                {
                    차량종류 = row.차량종류,
                    기본운임 = row.기본운임,
                    Km당단가 = row.Km당단가,
                    최소운임 = row.최소운임,
                    야간할증 = row.야간할증,
                    우천할증 = row.우천할증,
                    CreatedAt = DateTime.UtcNow,
                    UpdatedAt = DateTime.UtcNow
                });
            }

            var 기존차량제원목록 = await db.차량제원.ToListAsync();
            var 기존차량코드셋 = new HashSet<string>(기존차량제원목록.Select(x => x.차량코드), StringComparer.OrdinalIgnoreCase);
            foreach (var (spec, index) in 차량제원시드.Select((value, index) => (value, index)))
            {
                ApplyVehicleRecommendationDefaults(spec, index + 1);

                if (기존차량코드셋.Contains(spec.차량코드))
                {
                    var existing = 기존차량제원목록.First(x => string.Equals(x.차량코드, spec.차량코드, StringComparison.OrdinalIgnoreCase));
                    existing.권장최대CBM = existing.권장최대CBM.GetValueOrDefault() > 0 ? existing.권장최대CBM : spec.권장최대CBM;
                    existing.추천우선순위 = existing.추천우선순위 > 0 ? existing.추천우선순위 : spec.추천우선순위;
                    existing.추천사용여부 = existing.추천사용여부;
                    existing.UpdatedAt = DateTime.UtcNow;
                    continue;
                }

                db.차량제원.Add(spec);
            }

            await db.SaveChangesAsync();
        }

        private static async Task EnsureOrdererProfileAsync(HongdalContext db, ApplicationUser user, string displayName, string phoneNumber)
        {
            var profile = await db.주문자프로필.FirstOrDefaultAsync(x => x.UserId == user.Id);
            if (profile == null)
            {
                db.주문자프로필.Add(new 주문자프로필
                {
                    UserId = user.Id,
                    표시명 = displayName,
                    연락처 = phoneNumber,
                    기본주소 = string.Empty,
                    CreatedAt = DateTime.UtcNow,
                    UpdatedAt = DateTime.UtcNow
                });

                return;
            }

            profile.표시명 = string.IsNullOrWhiteSpace(profile.표시명) ? displayName : profile.표시명;
            profile.연락처 = string.IsNullOrWhiteSpace(profile.연락처) ? phoneNumber : profile.연락처;
            profile.기본주소 ??= string.Empty;
            profile.UpdatedAt = DateTime.UtcNow;
        }

        private static void ApplyVehicleRecommendationDefaults(차량제원 spec, int priority)
        {
            spec.권장최대CBM = CalculateRecommendedCbm(spec);
            spec.추천우선순위 = priority;
            spec.추천사용여부 = true;
        }

        private static decimal? CalculateRecommendedCbm(차량제원 spec)
        {
            if (spec.적재함길이Mm <= 0 || spec.적재함폭Mm <= 0 || spec.적재함높이Mm.GetValueOrDefault() <= 0)
            {
                return null;
            }

            var cbm = (spec.적재함길이Mm / 1000m)
                      * (spec.적재함폭Mm / 1000m)
                      * (spec.적재함높이Mm!.Value / 1000m)
                      * 0.8m;
            return decimal.Round(cbm, 3, MidpointRounding.AwayFromZero);
        }
    }
}
