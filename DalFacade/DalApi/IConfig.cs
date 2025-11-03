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
    TimeSpan MinTimeDelivery { get; set; }
    double MaxSpeedDelivery { get; set; }

    void Reset();
}
