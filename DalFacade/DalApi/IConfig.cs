using DO;

namespace DalApi;

public interface IConfig
{
    DateTime Clock { get; set; }
    int BossId { get; set; }
    string BossPassword { get; set; }
    double CarSpeed { get; set; }
    double MotorcycleSpeed { get; set; }
    double BikeSpeed { get; set; }
    double WalkingSpeed { get; set; }
    TimeSpan MaxTimeDelivery { get; set; }
    TimeSpan RiskRange { get; set; }
    TimeSpan Inactivity { get; set; }
    string CompanyAdress { get; set; }
    double Latitude { get; set; }
    double Longitude { get; set; }
    double MaxDistance { get; set; }


    void Reset();
}
