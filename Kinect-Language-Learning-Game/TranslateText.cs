using System;
using System.IO;
using System.Net;

namespace ColorBasics
{
    public class TranslateText
    {
        private static string SubscriptionKey = "insert key here";

        private static string GetToken()
        {
            string uri = "https://api.cognitive.microsoft.com/sts/v1.0/issueToken?Subscription-Key=" + SubscriptionKey;
            HttpWebRequest httpWebRequest = (HttpWebRequest)WebRequest.Create(uri);
            httpWebRequest.Method = "POST";
            httpWebRequest.ContentLength = 0;

            try
            {
                using (HttpWebResponse response = (HttpWebResponse) httpWebRequest.GetResponse())
                {
                    if (HttpStatusCode.OK != response.StatusCode)
                    {
                        return null;
                    }

                    using (Stream dataStream = response.GetResponseStream())
                    {
                        using (StreamReader reader = new StreamReader(dataStream))
                        {
                            return reader.ReadToEnd();
                        }
                    }
                }
            }
            catch
            {
                return null;
            }
        }

        public static string Translate(string text)
        {
            string authToken = GetToken();
            if (null == authToken)
            {
                return null;
            }

            string from = "en";
            string to = "zh";   
            string uri = "http://api.microsofttranslator.com/v2/Http.svc/Translate?" + "from=" + from + "&to=" + to + "&contentType=text/plain&text=" + System.Web.HttpUtility.UrlEncode(text);
            HttpWebRequest httpWebRequest = (HttpWebRequest)WebRequest.Create(uri);
            httpWebRequest.Headers.Add("Authorization", "Bearer " + authToken);
            HttpWebResponse response = null;
            try
            {
                response = (HttpWebResponse) httpWebRequest.GetResponse();
                using (Stream stream = response.GetResponseStream())
                {
                    System.Runtime.Serialization.DataContractSerializer dcs = new System.Runtime.Serialization.DataContractSerializer(Type.GetType("System.String"));
                    return (string)dcs.ReadObject(stream);
                }
            }
            catch (Exception e)
            {
                return null;
            }
            finally
            {
                if (response != null)
                {
                    response.Close();
                    response = null;
                }
            }
        }
    }
}
