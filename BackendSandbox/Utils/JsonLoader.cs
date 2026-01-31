using System.Text.Json;

namespace BackendSandbox.Utils;

public class JsonLoader
{
    public static T LoadJsonc<T>(string path)
    {
        string jsonString = File.ReadAllText(path);

        var options = new JsonSerializerOptions
        {
            ReadCommentHandling = JsonCommentHandling.Skip,
            AllowTrailingCommas = true,
            PropertyNameCaseInsensitive = true,
        };
        
        return JsonSerializer.Deserialize<T>(jsonString, options);
    }
}