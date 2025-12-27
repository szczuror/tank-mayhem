namespace Client;

public static class GameConstants
{
    public const float TankSpeed = 4.5f;
    public const float TankRotationSpeed = 0.025f;
    public const float TurretRotationSpeedDegrees = 90f;
    public const float TankRadius = 150f;
    public const float BarrelLength = 150f;
    public const int DamageAmount = 35;
    public const float BulletSpeed = 20f;
    public const float BulletLifetime = 4.0f;
    public const int MaxNicknameLength = 15;
    private const float NetSendInterval = 0.05f;
    public const float RotationThresholdDegrees = 0.5f;
    public const float PositionThreshold = 1.0f;
    public const float ShakeIntensityOnShoot = 35f;
    public const float ShakeDamping = 0.9f;
    public const float ShotVolume = 0.5f;
    public const float ShotPitchVariation = 0.2f;
    public const int MinSpawnX = 500;
    public const int MinSpawnY = 500;
    public const int RespawnMinX = 100;
    public const int RespawnMaxX = 1280;
    public const int RespawnMinY = 100;
    public const int RespawnMaxY = 720;
    public const float KillFeedDisplayTime = 5.0f;
    public const float RecoilDamping = 0.85f;
    public const float Smoothing = 0.15f;
    
    // Improved interpolation constants
    // MaxExtrapolationTime: Maximum time to predict movement before falling back to direct interpolation
    // Adaptive smoothing: Adjust interpolation speed based on distance from target
    public const float MaxExtrapolationTime = 0.5f; // Max time to predict ahead
    public const float MinSmoothingFactor = 0.1f; // Faster interpolation when far from target
    public const float MaxSmoothingFactor = 0.3f; // Slower interpolation when close to target
    public const float SmoothingDistanceThreshold = 100f; // Distance at which to adjust smoothing
}
