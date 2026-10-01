namespace HongdalApp.Services.Customs;

public interface ICustomsBrokerDirectory
{
    IReadOnlyList<CustomsBrokerProfile> GetAvailableBrokers();
}
