using System.Text.Json.Serialization;

public class Stats
{
    [JsonPropertyName("maxHp")]
    public int MaxHp { get; set; }

    [JsonPropertyName("atk")]
    public int Atk { get; set; }

    [JsonPropertyName("def")]
    public int Def { get; set; }

    [JsonPropertyName("magicResistance")]
    public float MagicResistance { get; set; }

    [JsonPropertyName("cost")]
    public int Cost { get; set; }

    [JsonPropertyName("blockCnt")]
    public int BlockCnt { get; set; }

    [JsonPropertyName("respawnTime")]
    public int RespawnTime { get; set; }
}