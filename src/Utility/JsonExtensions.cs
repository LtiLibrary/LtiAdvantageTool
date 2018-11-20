using Newtonsoft.Json;

namespace AdvantageTool.Utility
{
    public static class JsonExtensions
    {
        public static string ToJsonString<T>(this T obj)
        {
            return JsonConvert.SerializeObject(obj, Formatting.Indented,
                new JsonSerializerSettings
                {
                    NullValueHandling = NullValueHandling.Ignore
                });
        }
    }
}
