using System;
using System.IO;
using System.Net;
using System.Runtime.Serialization.Json;
using System.Runtime.Serialization;
using System.Web;
using System.Media;

namespace Microsoft.Samples.Kinect.ColorBasics
{
    public class TranslateText
    {
        private static string SubscriptionKey = "<put your token here>";

        private static string GetToken()
        {
            string uri = "https://api.cognitive.microsoft.com/sts/v1.0/issueToken?Subscription-Key=" + subscriptionKey;
            HttpWebRequest httpWebRequest = (HttpWebRequest)WebRequest.Create(uri);
            httpWebRequest.Method = "POST";
            httpWebRequest.ContentLength = 0;

            try
            {
                using (HttpWebResponse response = httpWebRequest.GetResponse())
                {
                    if (200 != response.StatusCode)
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
                response = httpWebRequest.GetResponse();
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
