namespace Dal;

internal static class Config
(
    
    int NextOrderId,
    int NextDeliveryId,
    DateTime Clock,
    int BossId,
    string BossPasword, // a redefinir tosefet
    string? CompanyAdress = null,
    double? Latitude = null,
    double? Longitude = null,
    double? MaxDistance = null,
    double SpeedOfCar,
    double SpeedOfMotorBike,
    double speedOfBike,
    double SpeedOfWalking,
    TimeSpan MaxDeliveryTimeRange,
    TimeSpan RiskRange,
    TimeSpan InactivityTime
)
{

}

