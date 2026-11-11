/// <summary>Bölge 4–6 mekanikleri için basit sayaçlar (telemetri/test). Savaş başında sıfırlanır.</summary>
public static class RegionMechanicsStats
{
    public static int Thaws;
    public static int ShrinesDestroyed;
    public static float EruptionDamageToEnemies;
    public static float EruptionDamageToPlayer;

    public static void Reset()
    {
        Thaws = 0;
        ShrinesDestroyed = 0;
        EruptionDamageToEnemies = 0f;
        EruptionDamageToPlayer = 0f;
    }
}
