using System.Text.Json.Serialization;

namespace KMRLauncherMvvm.Models;

public class Resources
{
    [JsonPropertyName("homepage")]
    public string Homepage { get; set; }
    
    [JsonPropertyName("bugtracker")]
    public string Bugtracker { get; set; }
    
    [JsonPropertyName("discussions")]
    public string Discussions { get; set; }
    
    [JsonPropertyName("license")]
    public string License { get; set; }
    
    [JsonPropertyName("repository")]
    public string Repository { get; set; }
    
    [JsonPropertyName("ci")]
    public string Ci { get; set; }
    
    [JsonPropertyName("spacedock")]
    public string Spacedock { get; set; }
    
    [JsonPropertyName("curse")]
    public string Curse { get; set; }
    
    [JsonPropertyName("manual")]
    public string Manual { get; set; }
    
    [JsonPropertyName("metanetkan")]
    public string Metanetkan { get; set; }
    
    [JsonPropertyName("remote-avc")]
    public string RemoteAvc { get; set; }
    
    [JsonPropertyName("remote-swinfo")]
    public string RemoteSwinfo { get; set; }
    
    [JsonPropertyName("store")]
    public string Store { get; set; }
    
    [JsonPropertyName("steamstore")]
    public string Steamstore { get; set; }
    
    [JsonPropertyName("gogstore")]
    public string Gogstore { get; set; }
    
    [JsonPropertyName("epicstore")]
    public string Epicstore { get; set; }
}