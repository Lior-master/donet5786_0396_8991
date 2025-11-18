using DalApi;

namespace Dal;

internal class ConfigImplementation : IConfig
{
    public DateTime Clock { get => Config.Clock; set => Config.Clock = value; }

    public int BossId { get => Config.BossId; set => Config.BossId = value; }

    public string BossPassword { get => Config.BossPassword; set => Config.BossPassword = value; }

    public double CarSpeed { get => Config.CarSpeed; set => Config.CarSpeed = value; }

    public double MotorcycleSpeed { get => Config.MotorcycleSpeed; set => Config.MotorcycleSpeed = value; }

    public double BikeSpeed { get => Config.BikeSpeed; set => Config.BikeSpeed = value; }

    public double WalkingSpeed { get => Config.WalkingSpeed; set => Config.WalkingSpeed = value; }

    public TimeSpan MaxTimeDelivery { get => Config.MaxTimeDelivery; set => Config.MaxTimeDelivery = value; }

    public TimeSpan RiskRange { get => Config.RiskRange; set => Config.RiskRange = value; }

    public TimeSpan Inactivity { get => Config.Inactivity; set => Config.Inactivity = value; }

    public string CompanyAdress { get => Config.CompanyAddress ?? string.Empty; set => Config.CompanyAddress = value; }
    public double Latitude { get => Config.Latitude ?? 0.0; set => Config.Latitude = value; }
    public double Longitude { get => Config.Longitude ?? 0.0; set => Config.Longitude = value; }
    public double MaxDistance { get => Config.MaxDistance ?? 0.0; set => Config.MaxDistance = value; }


    public void Reset()
    {
        Config.Reset();
    }
}
