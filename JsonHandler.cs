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
                Console.WriteLine("JSON handler: select token " + token);
                Console.WriteLine("Which is: " + (string)obj.SelectToken(token));
                //Console.WriteLine("data.payload: " + obj.data.payload);

                return (string)obj.SelectToken(token); //"data[0].payload"
            }
            catch (JsonReaderException jre)
            {
                LogWriter.Write("JSON Parsing error: "+jre.Message, LogWriter.LogEventType.Error);
                Console.WriteLine("Problems parsing JSON: "+jre.Message);
                return null;
            }
        }

        /*public static void ReadJson(string json)
        {
            JsonTextReader reader = new JsonTextReader(new StringReader(json));
            while (reader.Read())
            {
                if (reader.Value != null)
                {
                    Console.WriteLine("Token: {0}, Value: {1}", reader.TokenType, reader.Value);
                }
                else
                {
                    Console.WriteLine("Token: {0}", reader.TokenType);
                }
            }
        }*/
    }
}
