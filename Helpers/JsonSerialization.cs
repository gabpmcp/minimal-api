using Newtonsoft.Json;

namespace MinimalApi.Helpers {
    public static class JsonSerialization
    {
        public static async Task<T> DeserializeAsync<T>(Stream stream)
        {
            using var reader = new StreamReader(stream);
            var content = await reader.ReadToEndAsync();
            return JsonConvert.DeserializeObject<T>(content);
        }

        public static async Task SerializeAsync<T>(Stream stream, T value)
        {
            using var writer = new StreamWriter(stream);
            var content = JsonConvert.SerializeObject(value);
            await writer.WriteAsync(content);
        }
        
        public static T Deserialize<T>(string content) where T : new() => JsonConvert.DeserializeObject<T>(content) ?? new T();

        public static string Serialize<T>(T obj) => JsonConvert.SerializeObject(obj);
    }
}
