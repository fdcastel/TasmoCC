using Newtonsoft.Json;

namespace TasmoCC.Service
{
    public static class StringExtensions
    {
        public static T DeserializeIgnoringCase<T>(this string json) => JsonConvert.DeserializeObject<T>(json);
    }
}
