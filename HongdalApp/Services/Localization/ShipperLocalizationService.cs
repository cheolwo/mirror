using Hongdal.Contracts.Common.Localization;

namespace HongdalApp.Services.Localization;

public sealed class ShipperLocalizationService
{
    private const string PreferredLanguageKey = "shipper.preferred_language";

    private static readonly IReadOnlyDictionary<string, IReadOnlyDictionary<string, string>> Resources =
        new Dictionary<string, IReadOnlyDictionary<string, string>>(StringComparer.OrdinalIgnoreCase)
        {
            ["ko"] = new Dictionary<string, string>(StringComparer.Ordinal)
            {
                ["Language.Korean"] = "\uD55C\uAD6D\uC5B4",
                ["Language.English"] = "English",
                ["Language.DisplayLanguage"] = "\uD45C\uC2DC \uC5B8\uC5B4",
                ["ProfileSettings.Nav"] = "\uD654\uC8FC \uD504\uB85C\uD544",
                ["ProfileSettings.PageTitle"] = "\uD654\uC8FC \uD504\uB85C\uD544 \uC124\uC815",
                ["ProfileSettings.Title"] = "\uD654\uC8FC \uD504\uB85C\uD544 \uC124\uC815",
                ["ProfileSettings.Description"] = "\uD654\uC8FC\uAC00 \uAD6D\uB0B4\uC5D0 \uC788\uB294\uC9C0, \uAD6D\uC678\uC5D0 \uC788\uB294\uC9C0\uC5D0 \uB530\uB77C \uAE30\uBCF8 \uBA54\uB274\uB97C \uC870\uC815\uD569\uB2C8\uB2E4.",
                ["ProfileSettings.LocationTitle"] = "\uD654\uC8FC \uC704\uCE58",
                ["ProfileSettings.Domestic"] = "\uAD6D\uB0B4 \uD654\uC8FC",
                ["ProfileSettings.DomesticDescription"] = "\uD55C\uAD6D \uB0B4 \uC6B4\uC1A1, \uBC30\uCC28, \uD310\uB9E4 \uC5F0\uB3D9 \uC911\uC2EC\uC73C\uB85C \uBCF4\uC5EC\uC90D\uB2C8\uB2E4.",
                ["ProfileSettings.Overseas"] = "\uAD6D\uC678 \uD654\uC8FC",
                ["ProfileSettings.OverseasDescription"] = "\uD574\uC678\uC5D0\uC11C \uD55C\uAD6D\uC73C\uB85C \uC758\uB8B0\uD558\uB294 \uC218\uC785, \uC785\uACE0, \uBB3C\uB958 \uD750\uB984 \uC911\uC2EC\uC73C\uB85C \uBCF4\uC5EC\uC90D\uB2C8\uB2E4.",
                ["ProfileSettings.CurrentPolicy"] = "\uD604\uC7AC \uBA54\uB274 \uAE30\uC900",
                ["ProfileSettings.DomesticPolicy"] = "\uAD6D\uB0B4 \uD654\uC8FC: \uC785\uACE0 \uB300\uC2DC\uBCF4\uB4DC/\uC785\uACE0 \uD604\uD669 \uBA54\uB274\uB97C \uAE30\uBCF8\uC73C\uB85C \uC228\uAE41\uB2C8\uB2E4.",
                ["ProfileSettings.OverseasPolicy"] = "\uAD6D\uC678 \uD654\uC8FC: \uD310\uB9E4\uCC44\uB110/\uCD9C\uD488 \uAD00\uB9AC \uBA54\uB274\uB97C \uAE30\uBCF8\uC73C\uB85C \uC228\uAE41\uB2C8\uB2E4.",
                ["Layout.About"] = "About",
                ["Nav.DispatchAddress"] = "\uC0C1\uCC28/\uD558\uCC28 \uC8FC\uC18C \uC785\uB825",
                ["Nav.Home"] = "Home",
                ["Nav.Request"] = "\uD654\uBB3C\uC6B4\uC1A1\uC758\uB8B0 \uB4F1\uB85D",
                ["Nav.PublicCargo"] = "\uACF5\uAC1C \uD654\uBB3C\uC815\uBCF4",
                ["Nav.ExplorationInbox"] = "\uBC1B\uC740 \uD0D0\uC0C9 \uBB38\uC758\uD568",
                ["Nav.InboundDashboard"] = "\uC785\uACE0 \uB300\uC2DC\uBCF4\uB4DC",
                ["Nav.InboundRequests"] = "\uC785\uACE0 \uD604\uD669",
                ["Nav.WarehouseInventory"] = "\uC7AC\uACE0 \uBAA9\uB85D",
                ["Nav.ReconsignmentOrders"] = "\uC7AC\uC704\uD0C1 \uC6B4\uC1A1",
                ["Nav.SalesChannels"] = "\uD310\uB9E4\uCC44\uB110 \uC5F0\uACB0",
                ["Nav.ProductListings"] = "\uCD9C\uD488 \uAD00\uB9AC",
                ["Nav.ViewSettings"] = "\uD654\uBA74 \uC124\uC815",
                ["Nav.Group.Primary"] = "홈",
                ["Nav.Group.Transport"] = "운송",
                ["Nav.Group.Warehouse"] = "창고/재고",
                ["Nav.Group.Sales"] = "판매",
                ["Nav.Group.Settings"] = "설정",
                ["Home.PageTitle"] = "살뜰",
                ["Home.Title"] = "살뜰 업무 홈",
                ["Home.Subtitle"] = "\uC2E4\uC81C \uB85C\uADF8\uC778 \uC138\uC158\uACFC \uC11C\uBC84 \uC815\uCC45\uC744 \uAE30\uC900\uC73C\uB85C \uC5C5\uBB34 \uD654\uBA74\uC744 \uC81C\uACF5\uD569\uB2C8\uB2E4.",
                ["Home.Refresh"] = "\uC0C8\uB85C\uACE0\uCE68",
                ["Home.CreateRequest"] = "\uD654\uBB3C\uC6B4\uC1A1\uC758\uB8B0 \uB4F1\uB85D",
                ["Home.Console.Payment.Title"] = "결제 확인",
                ["Home.Console.Payment.Description"] = "결제 대기 의뢰를 확인하고 다음 단계로 넘깁니다.",
                ["Home.Console.Payment.Action"] = "의뢰 확인",
                ["Home.Console.Dispatch.Title"] = "배차 확인",
                ["Home.Console.Dispatch.Description"] = "배차 대기 또는 매칭 중인 운송을 확인합니다.",
                ["Home.Console.Dispatch.Action"] = "배차 보기",
                ["Home.Console.Inbound.Title"] = "입고 처리",
                ["Home.Console.Inbound.Description"] = "입고예정 상품을 확인하고 재고로 전환합니다.",
                ["Home.Console.Inbound.Action"] = "입고 처리",
                ["Home.Console.Inventory.Title"] = "재고 후속 업무",
                ["Home.Console.Inventory.Description"] = "가용 재고의 재위탁 또는 판매 등록을 진행합니다.",
                ["Home.Console.Inventory.Action"] = "재고 보기",
                ["Home.CommonContent"] = "\uD64D\uB2EC \uACF5\uD1B5 \uCF58\uD150\uCE20",
                ["Home.Benefit"] = "\uD61C\uD0DD",
                ["Home.Notice"] = "\uACF5\uC9C0",
                ["Home.LoginTitle"] = "\uD654\uC8FC \uB85C\uADF8\uC778",
                ["Home.LoginDescription"] = "\uD654\uC8FC \uACC4\uC815\uC73C\uB85C \uB85C\uADF8\uC778\uD558\uBA74 \uD654\uBA74 \uC124\uC815\uACFC \uC0AC\uC6A9\uC790 \uD589\uC704\uAC00 \uC2E4\uC81C \uACC4\uC815 \uAE30\uC900\uC73C\uB85C \uB3D9\uAE30\uD654\uB429\uB2C8\uB2E4.",
                ["Home.LoginId"] = "\uC544\uC774\uB514 \uB610\uB294 \uC774\uBA54\uC77C",
                ["Home.Password"] = "\uBE44\uBC00\uBC88\uD638",
                ["Home.Login"] = "\uB85C\uADF8\uC778",
                ["Home.LoggingIn"] = "\uB85C\uADF8\uC778 \uC911...",
                ["Home.NextAction"] = "\uB2E4\uC74C \uD589\uB3D9",
                ["Home.CurrentUser"] = "\uD604\uC7AC \uC0AC\uC6A9\uC790",
                ["Home.NotLoggedIn"] = "\uBBF8\uB85C\uADF8\uC778",
                ["Home.Logout"] = "\uB85C\uADF8\uC544\uC6C3",
                ["Home.WorkflowSteps"] = "\uC5C5\uBB34 \uB2E8\uACC4",
                ["Home.CurrentStep"] = "\uD604\uC7AC \uB2E8\uACC4",
                ["Home.Completed"] = "\uC644\uB8CC",
                ["Home.NextStep"] = "\uB2E4\uC74C \uB2E8\uACC4",
                ["Home.RequestList"] = "\uC758\uB8B0 \uBAA9\uB85D",
                ["Home.NoRequests"] = "\uC758\uB8B0\uAC00 \uC5C6\uC2B5\uB2C8\uB2E4.",
                ["Home.CargoType"] = "\uD654\uBB3C\uC885\uB958",
                ["Home.Pickup"] = "\uD53D\uC5C5\uC9C0",
                ["Home.Status"] = "\uC0C1\uD0DC",
                ["Home.Payment"] = "\uACB0\uC81C",
                ["Home.Dispatch"] = "\uBC30\uCC28",
                ["Home.PaymentMethod"] = "\uACB0\uC81C\uC218\uB2E8",
                ["Home.CreatedAt"] = "\uB4F1\uB85D\uC77C\uC2DC",
                ["Home.Pay"] = "\uACB0\uC81C\uD558\uAE30",
                ["Home.Validation.LoginRequired"] = "\uC544\uC774\uB514(\uB610\uB294 \uC774\uBA54\uC77C)\uC640 \uBE44\uBC00\uBC88\uD638\uB97C \uC785\uB825\uD574 \uC8FC\uC138\uC694.",
                ["Home.Message.LoginSucceeded"] = "{0} \uB2D8, \uB85C\uADF8\uC778\uB418\uC5C8\uC2B5\uB2C8\uB2E4.",
                ["Home.Message.LoggedOut"] = "\uB85C\uADF8\uC544\uC6C3\uB418\uC5C8\uC2B5\uB2C8\uB2E4.",
                ["Home.WorkflowTitle"] = "\uD654\uC8FC \uC5C5\uBB34 \uD750\uB984",
                ["Home.Workflow.NoRequest"] = "\uC758\uB8B0 \uC5C6\uC74C",
                ["Home.Workflow.LatestRequest"] = "\uCD5C\uADFC \uC758\uB8B0: {0} / \uACB0\uC81C {1} / \uBC30\uCC28 {2}",
                ["Home.Action.NewRequest"] = "\uC0C8 \uD654\uBB3C \uC758\uB8B0",
                ["Home.Action.Pay"] = "\uACB0\uC81C\uD558\uAE30",
                ["Home.Action.CheckDispatch"] = "\uBC30\uCC28 \uD655\uC778",
                ["Home.Action.TransportStatus"] = "\uC6B4\uC1A1 \uC0C1\uD0DC",
                ["Home.Action.DeliveryComplete"] = "\uBC30\uC1A1 \uC644\uB8CC",
                ["Home.Action.AllRequests"] = "\uB0B4 \uC758\uB8B0 \uC804\uCCB4",
                ["Home.Action.CheckWork"] = "\uC5C5\uBB34 \uD655\uC778",
                ["Home.Step.Request.Title"] = "\uC0C8 \uD654\uBB3C \uC758\uB8B0",
                ["Home.Step.Request.Description"] = "\uC758\uB8B0\uB97C \uC791\uC131\uD558\uACE0 \uB4F1\uB85D\uD569\uB2C8\uB2E4.",
                ["Home.Step.Payment.Title"] = "\uACB0\uC81C\uD558\uAE30",
                ["Home.Step.Payment.Description"] = "\uC608\uC0C1 \uC6B4\uC784\uC744 \uD655\uC778\uD558\uACE0 \uACB0\uC81C\uB97C \uC9C4\uD589\uD569\uB2C8\uB2E4.",
                ["Home.Step.Dispatch.Title"] = "\uBC30\uCC28 \uD655\uC778",
                ["Home.Step.Dispatch.Description"] = "\uBC30\uCC28 \uC9C4\uD589\uACFC \uAE30\uC0AC \uBC30\uC815\uC744 \uD655\uC778\uD569\uB2C8\uB2E4.",
                ["Home.Step.Transport.Title"] = "\uC6B4\uC1A1 \uC0C1\uD0DC",
                ["Home.Step.Transport.Description"] = "\uC0C1\uCC28\uC640 \uC6B4\uC1A1 \uC0C1\uD0DC\uB97C \uD655\uC778\uD569\uB2C8\uB2E4.",
                ["Home.Step.Complete.Title"] = "\uBC30\uC1A1 \uC644\uB8CC",
                ["Home.Step.Complete.Description"] = "\uB3C4\uCC29\uACFC \uC99D\uBE59\uC744 \uD655\uC778\uD569\uB2C8\uB2E4.",
                ["Home.Step.History.Title"] = "\uB0B4 \uC758\uB8B0 \uC804\uCCB4",
                ["Home.Step.History.Description"] = "\uACFC\uAC70 \uC758\uB8B0\uC640 \uC608\uC678 \uC0C1\uD0DC\uB97C \uD655\uC778\uD569\uB2C8\uB2E4.",
                ["Home.Alert.PaymentTitle"] = "\uACB0\uC81C",
                ["Home.Alert.PaymentMessage"] = "{0} \uACB0\uC81C \uC9C4\uC785 \uBC84\uD2BC\uC785\uB2C8\uB2E4.",
                ["Home.Alert.Confirm"] = "\uD655\uC778"
            },
            ["en"] = new Dictionary<string, string>(StringComparer.Ordinal)
            {
                ["Language.Korean"] = "Korean",
                ["Language.English"] = "English",
                ["Language.DisplayLanguage"] = "Display Language",
                ["ProfileSettings.Nav"] = "Shipper Profile",
                ["ProfileSettings.PageTitle"] = "Shipper Profile Settings",
                ["ProfileSettings.Title"] = "Shipper Profile Settings",
                ["ProfileSettings.Description"] = "Adjust the default menu based on whether the shipper operates in Korea or overseas.",
                ["ProfileSettings.LocationTitle"] = "Shipper Location",
                ["ProfileSettings.Domestic"] = "Domestic Shipper",
                ["ProfileSettings.DomesticDescription"] = "Focuses on Korea-based transport, dispatch, and sales integration.",
                ["ProfileSettings.Overseas"] = "Overseas Shipper",
                ["ProfileSettings.OverseasDescription"] = "Focuses on overseas-to-Korea requests, inbound logistics, and import flows.",
                ["ProfileSettings.CurrentPolicy"] = "Current Menu Policy",
                ["ProfileSettings.DomesticPolicy"] = "Domestic shipper: inbound dashboard and inbound requests are hidden by default.",
                ["ProfileSettings.OverseasPolicy"] = "Overseas shipper: sales channels and listings are hidden by default.",
                ["Layout.About"] = "About",
                ["Nav.DispatchAddress"] = "Pickup/Delivery Address",
                ["Nav.Home"] = "Home",
                ["Nav.Request"] = "Create Shipment Request",
                ["Nav.PublicCargo"] = "Public Cargo",
                ["Nav.ExplorationInbox"] = "Inbound Inquiries",
                ["Nav.InboundDashboard"] = "Inbound Dashboard",
                ["Nav.InboundRequests"] = "Inbound Requests",
                ["Nav.WarehouseInventory"] = "Inventory",
                ["Nav.ReconsignmentOrders"] = "Reconsignment",
                ["Nav.SalesChannels"] = "Sales Channels",
                ["Nav.ProductListings"] = "Listings",
                ["Nav.ViewSettings"] = "View Settings",
                ["Nav.Group.Primary"] = "Home",
                ["Nav.Group.Transport"] = "Transport",
                ["Nav.Group.Warehouse"] = "Warehouse",
                ["Nav.Group.Sales"] = "Sales",
                ["Nav.Group.Settings"] = "Settings",
                ["Home.PageTitle"] = "Hongdal App",
                ["Home.Title"] = "Hongdal App Workspace",
                ["Home.Subtitle"] = "Work screens are shown based on the active login session and server policy.",
                ["Home.Refresh"] = "Refresh",
                ["Home.CreateRequest"] = "Create Shipment Request",
                ["Home.Console.Payment.Title"] = "Payment Review",
                ["Home.Console.Payment.Description"] = "Review shipment requests waiting for payment.",
                ["Home.Console.Payment.Action"] = "Review Requests",
                ["Home.Console.Dispatch.Title"] = "Dispatch Review",
                ["Home.Console.Dispatch.Description"] = "Check shipments waiting for dispatch or matching.",
                ["Home.Console.Dispatch.Action"] = "View Dispatch",
                ["Home.Console.Inbound.Title"] = "Inbound Work",
                ["Home.Console.Inbound.Description"] = "Process expected inbound items into inventory.",
                ["Home.Console.Inbound.Action"] = "Process Inbound",
                ["Home.Console.Inventory.Title"] = "Inventory Actions",
                ["Home.Console.Inventory.Description"] = "Move available inventory into reconsignment or sales.",
                ["Home.Console.Inventory.Action"] = "View Inventory",
                ["Home.CommonContent"] = "Hongdal Updates",
                ["Home.Benefit"] = "Benefit",
                ["Home.Notice"] = "Notice",
                ["Home.LoginTitle"] = "Shipper Login",
                ["Home.LoginDescription"] = "Sign in with a shipper account to sync view settings and user activity.",
                ["Home.LoginId"] = "ID or Email",
                ["Home.Password"] = "Password",
                ["Home.Login"] = "Log In",
                ["Home.LoggingIn"] = "Logging in...",
                ["Home.NextAction"] = "Next Action",
                ["Home.CurrentUser"] = "Current User",
                ["Home.NotLoggedIn"] = "Not signed in",
                ["Home.Logout"] = "Log Out",
                ["Home.WorkflowSteps"] = "Workflow Steps",
                ["Home.CurrentStep"] = "Current Step",
                ["Home.Completed"] = "Completed",
                ["Home.NextStep"] = "Next Step",
                ["Home.RequestList"] = "Request List",
                ["Home.NoRequests"] = "No requests yet.",
                ["Home.CargoType"] = "Cargo Type",
                ["Home.Pickup"] = "Pickup",
                ["Home.Status"] = "Status",
                ["Home.Payment"] = "Payment",
                ["Home.Dispatch"] = "Dispatch",
                ["Home.PaymentMethod"] = "Payment Method",
                ["Home.CreatedAt"] = "Created At",
                ["Home.Pay"] = "Pay",
                ["Home.Validation.LoginRequired"] = "Please enter your ID or email and password.",
                ["Home.Message.LoginSucceeded"] = "{0}, you are signed in.",
                ["Home.Message.LoggedOut"] = "You have been signed out.",
                ["Home.WorkflowTitle"] = "Shipper Workflow",
                ["Home.Workflow.NoRequest"] = "No request",
                ["Home.Workflow.LatestRequest"] = "Latest request: {0} / payment {1} / dispatch {2}",
                ["Home.Action.NewRequest"] = "New Shipment Request",
                ["Home.Action.Pay"] = "Pay",
                ["Home.Action.CheckDispatch"] = "Check Dispatch",
                ["Home.Action.TransportStatus"] = "Transport Status",
                ["Home.Action.DeliveryComplete"] = "Delivery Complete",
                ["Home.Action.AllRequests"] = "All Requests",
                ["Home.Action.CheckWork"] = "Check Work",
                ["Home.Step.Request.Title"] = "New Shipment Request",
                ["Home.Step.Request.Description"] = "Create and submit a shipment request.",
                ["Home.Step.Payment.Title"] = "Pay",
                ["Home.Step.Payment.Description"] = "Review the estimated freight charge and proceed with payment.",
                ["Home.Step.Dispatch.Title"] = "Check Dispatch",
                ["Home.Step.Dispatch.Description"] = "Track dispatch progress and driver assignment.",
                ["Home.Step.Transport.Title"] = "Transport Status",
                ["Home.Step.Transport.Description"] = "Check pickup and transport status.",
                ["Home.Step.Complete.Title"] = "Delivery Complete",
                ["Home.Step.Complete.Description"] = "Confirm arrival and proof of delivery.",
                ["Home.Step.History.Title"] = "All Requests",
                ["Home.Step.History.Description"] = "Review past requests and exception statuses.",
                ["Home.Alert.PaymentTitle"] = "Payment",
                ["Home.Alert.PaymentMessage"] = "Payment entry point for {0}.",
                ["Home.Alert.Confirm"] = "OK"
            }
        };

    public event Action? Changed;

    public ShipperLocalizationService()
    {
        var savedLanguage = Preferences.Default.Get(PreferredLanguageKey, "ko");
        Language = Resources.ContainsKey(savedLanguage) ? savedLanguage : "ko";
    }

    public string Language { get; private set; }
    public string DisplayLanguageCode => Language == "en"
        ? DisplayLanguageCodes.English
        : DisplayLanguageCodes.Korean;

    public void SetLanguage(string language)
    {
        if (!Resources.ContainsKey(language) || string.Equals(Language, language, StringComparison.OrdinalIgnoreCase))
        {
            return;
        }

        Language = language;
        Preferences.Default.Set(PreferredLanguageKey, language);
        Changed?.Invoke();
    }

    public bool TrySetDisplayLanguageCode(string? displayLanguageCode)
    {
        if (!DisplayLanguageCodes.TryNormalize(displayLanguageCode, out var normalizedCode))
        {
            return false;
        }

        SetLanguage(DisplayLanguageCodes.ToNeutralCode(normalizedCode));
        return true;
    }

    public bool IsLanguage(string language)
    {
        return string.Equals(Language, language, StringComparison.OrdinalIgnoreCase);
    }

    public string T(string key)
    {
        if (Resources.TryGetValue(Language, out var current) && current.TryGetValue(key, out var value))
        {
            return value;
        }

        return Resources["ko"].TryGetValue(key, out var fallback) ? fallback : key;
    }

    public string T(string key, params object?[] args)
    {
        return string.Format(T(key), args);
    }

    public string ViewName(string route, string fallback)
    {
        var key = route.Trim('/').ToLowerInvariant() switch
        {
            "" => "Nav.Home",
            "shipper/request" => "Nav.Request",
            "shipper/public-cargo" => "Nav.PublicCargo",
            "shipper/exploration/inbox" => "Nav.ExplorationInbox",
            "shipper/inbound/dashboard" => "Nav.InboundDashboard",
            "shipper/inbound/requests" => "Nav.InboundRequests",
            "shipper/warehouse/inventory" => "Nav.WarehouseInventory",
            "shipper/reconsignment/orders" => "Nav.ReconsignmentOrders",
            "shipper/sales/channels" => "Nav.SalesChannels",
            "shipper/sales/listings" => "Nav.ProductListings",
            "shipper/settings/views" => "Nav.ViewSettings",
            _ => null
        };

        return key is null ? fallback : T(key);
    }
}
