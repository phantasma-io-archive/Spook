using System;
using System.Net;

namespace LunarLabs.Parser.JSON
{
    public enum RequestType
    {
        GET,
        POST
    }

    public class JSONRPC_Client
    {
        private WebClient client;

        public JSONRPC_Client()
        {
            client = new WebClient() { Encoding = System.Text.Encoding.UTF8 }; 
        }

        public DataNode SendRequest(RequestType kind, string url, string method, params object[] parameters)
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
                client.Headers.Add("Content-Type", "application/json-rpc");

                switch (kind)
                {
                    case RequestType.GET:
                        {
                            contents = client.DownloadString(url); break;
                        }
                    case RequestType.POST:
                        {
                            var json = JSONWriter.WriteToString(jsonRpcData);
                            contents = client.UploadString(url, json);
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
            //Console.WriteLine(contents);

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
   }
}