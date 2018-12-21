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
        public static DataNode Execute(RequestType kind, string url, string method, params object[] parameters)
        {
            string contents;

            DataNode paramData;

            if (parameters!=null && parameters.Length > 0)
            {
                paramData = DataNode.CreateArray("params");
                foreach (var obj in parameters)
                {
                    paramData.AddField(null, obj);
                }
            }
            else
            {
                paramData = null;
            }

            var jsonRpcData = DataNode.CreateObject(null);
            jsonRpcData.AddField("jsonrpc", "2.0");
            jsonRpcData.AddField("method", method);
            jsonRpcData.AddField("id", "1");

            if (paramData != null)
            {
                jsonRpcData.AddNode(paramData);
            }

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
                            var json = JSONWriter.WriteToString(jsonRpcData);
                            contents = PostWebRequest(url, json);
                            break;
                        }
                    default: return null;
                }
            }
            catch (Exception e)
            {
                Console.WriteLine(e);
                return null;
            }

            if (string.IsNullOrEmpty(contents))
            {
                return null;
            }

            //File.WriteAllText("response.json", contents);
            Console.WriteLine(contents);

            var root = JSONReader.ReadFromString(contents);

            if (root == null)
            {
                return null;
            }

            if (root.HasNode("result"))
            {
                return root["result"];
            }

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