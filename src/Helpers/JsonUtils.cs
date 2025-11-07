using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization.Metadata;

namespace ParseM3UNet.Helpers
{
    public static class JsonUtils
    {

        public static string SerializeToBase64<T>(T o)
        {
            string json = System.Text.Json.JsonSerializer.Serialize(o);
            byte[] data = Encoding.UTF8.GetBytes(json);
            return Convert.ToBase64String(data);
        }

        public static T DeserializeFromBase64<T>(string b64)
        {
            byte[] data = Convert.FromBase64String(b64);
            string json = Encoding.UTF8.GetString(data);
            return System.Text.Json.JsonSerializer.Deserialize<T>(json)!;
        }

        public readonly static JsonSerializerOptions JsonOption = new JsonSerializerOptions
        {
            TypeInfoResolver = new DefaultJsonTypeInfoResolver
            {
                Modifiers =
                {
                    static typeInfo =>
                    {
                        if (typeInfo.Kind != JsonTypeInfoKind.Object)
                            return;

                        foreach (JsonPropertyInfo propertyInfo in typeInfo.Properties)
                        {
                            // Strip IsRequired constraint from every property.
                            propertyInfo.IsRequired = true;
                        }
                    }
                }
            }

        };
    }
}