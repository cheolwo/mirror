namespace HongdalApp.Services.Customs;

public sealed class SampleCustomsBrokerDirectory : ICustomsBrokerDirectory
{
    public IReadOnlyList<CustomsBrokerProfile> GetAvailableBrokers() =>
    [
        new() { BrokerId = "broker-furniture", BrokerName = "김관세사", Specialty = "가구/생활용품" },
        new() { BrokerId = "broker-food", BrokerName = "박관세사", Specialty = "식품/검역" },
        new() { BrokerId = "broker-electronics", BrokerName = "이관세사", Specialty = "전자기기" }
    ];
}
