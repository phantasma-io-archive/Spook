using System;
using System.Net;

namespace LunarLabs.Parser.JSON
{
    public enum RequestType
    {
        GET,
        POST
    }

    public static class JSONRequest
    {
        public static DataNode Execute(RequestType kind, string url, DataNode data = null)
        {
            string contents;

            try
            {
                switch (kind)
                {
                    case RequestType.GET:
                        {
                            contents = GetWebRequest(url); break;
                        }
                    case RequestType.POST:
                        {
                            var paramData = data != null ? JSONWriter.WriteToString(data) : "{}";
                            contents = PostWebRequest(url, paramData);
                            break;
                        }
                    default: return null;
                }
            }
            catch (Exception e)
            {
                return null;
            }

            if (string.IsNullOrEmpty(contents))
            {
                return null;
            }

            //File.WriteAllText("response.json", contents);

            var root = JSONReader.ReadFromString(contents);
            return root;
        }

        private static string GetWebRequest(string url)
        {
            using (var client = new WebClient { Encoding = System.Text.Encoding.UTF8 })
            {
                client.Headers.Add("Content-Type", "application/json-rpc");
                return client.DownloadString(url);
            }
        }

        private static string PostWebRequest(string url, string paramData)
        {
            using (var client = new WebClient { Encoding = System.Text.Encoding.UTF8 })
            {
                client.Headers.Add("Content-Type", "application/json-rpc");
                return client.UploadString(url, paramData);
            }
        }
    }
}