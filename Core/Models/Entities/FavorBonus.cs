using System.Text.Json.Serialization;

public class FavorBonus
{
    [JsonPropertyName("level")]
    public int Level { get; set; }

    [JsonPropertyName("data")]
    public FavorBonusData? Data { get; set; }
}

public class FavorBonusData
{
    [JsonPropertyName("maxHp")]
    public int MaxHp { get; set; }

    [JsonPropertyName("atk")]
    public int Atk { get; set; }

    [JsonPropertyName("def")]
    public int Def { get; set; }

    [JsonPropertyName("magicResistance")]
    public double MagicResistance { get; set; }
}