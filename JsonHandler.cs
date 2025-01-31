using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using System;

namespace NativeService
{
    class JsonHandler
    {
        public static string GetTokenValue(string json, string token)
        {
            try
            {
                JObject obj = JObject.Parse(json);

                LogWriter.Write($"JSON handler: selecting token '{token}'", LogWriter.LogEventType.Event);
                string value = obj.SelectToken(token)?.ToString();

                LogWriter.Write($"Token '{token}' value: {value}", LogWriter.LogEventType.Event);

                return value;
            }
            catch (JsonReaderException jre)
            {
                LogWriter.Write($"JSON parsing error: {jre.Message}", LogWriter.LogEventType.Error);
                return null;
            }
            catch (Exception e)
            {
                LogWriter.Write($"Unexpected error in JSON parsing: {e.Message}", LogWriter.LogEventType.Error);
                return null;
            }
        }
    }
}
