using System;
using System.Collections.Specialized;
using System.Net;
using System.IO;

namespace HTTPFileUploader
{
    class Program
    {

        public static Tuple<HttpStatusCode, string> HttpUploadFile(string url, string file, string paramName = "file", string contentType = "application/octet-stream", NameValueCollection nvc = null, NameValueCollection headers = null)
        {
            string result = "";
            string boundary = "---------------------------" + DateTime.Now.Ticks.ToString("x");
            byte[] boundarybytes = System.Text.Encoding.ASCII.GetBytes("\r\n--" + boundary + "\r\n");

            HttpWebRequest wr = (HttpWebRequest)WebRequest.Create(url);
            wr.Method = "POST";
            wr.KeepAlive = true;
            wr.Credentials = System.Net.CredentialCache.DefaultCredentials;
            wr.Accept = "text/html,application/xhtml+xml,application/xml;q=0.9,*/*;q=0.8";
            wr.Headers.Add("Accepts-Language", "en-us,en;q=0.5");
            wr.Headers.Add(headers);
            wr.ContentType = "multipart/form-data; boundary=" + boundary;

            int skip = 2;
            
            Stream rs = wr.GetRequestStream();

            string formdataTemplate = "Content-Disposition: form-data; name=\"{0}\"\r\n\r\n{1}";
            if (nvc != null)
            {

                foreach (string key in nvc.Keys)
                {
                    rs.Write(boundarybytes, skip, boundarybytes.Length - skip);
                    skip = 0;
                    string formitem = string.Format(formdataTemplate, key, nvc[key]);
                    byte[] formitembytes = System.Text.Encoding.UTF8.GetBytes(formitem);
                    rs.Write(formitembytes, 0, formitembytes.Length);
                }
            }

            rs.Write(boundarybytes, 0, boundarybytes.Length);
            skip = 0;

            string headerTemplate = "Content-Disposition: form-data; name=\"{0}\"; filename=\"{1}\"\r\nContent-Type: {2}\r\n\r\n";
            string header = string.Format(headerTemplate, paramName, new FileInfo(file).Name, contentType);
            byte[] headerbytes = System.Text.Encoding.UTF8.GetBytes(header);
            rs.Write(headerbytes, 0, headerbytes.Length);

            FileStream fileStream = new FileStream(file, FileMode.Open, FileAccess.Read);
            byte[] buffer = new byte[4096];
            int bytesRead = 0;
            while ((bytesRead = fileStream.Read(buffer, 0, buffer.Length)) != 0)
            {
                rs.Write(buffer, 0, bytesRead);
            }
            fileStream.Close();

            byte[] trailer = System.Text.Encoding.ASCII.GetBytes("\r\n--" + boundary + "--\r\n");
            rs.Write(trailer, 0, trailer.Length);
            rs.Close();

            WebResponse wresp = null;
            HttpStatusCode status = 0;

            try
            {
                wresp = wr.GetResponse();
                Stream stream2 = wresp.GetResponseStream();
                StreamReader reader2 = new StreamReader(stream2);
                result = reader2.ReadToEnd();
                status = ((HttpWebResponse)wresp).StatusCode;
            }
            catch (Exception ex)
            {
                if (wresp != null)
                {
                    wresp.Close();
                    wresp = null;
                }
                Console.Error.WriteLine("Error: {0} {1}", ex.GetType().FullName, ex.Message);
                status = (HttpStatusCode)600;
                result = "Client error";
            }
            finally
            {
                wr = null;
            }
            return new Tuple<HttpStatusCode, string>(status, result);
        }
    

    static void Main(string[] args)
        {
            string default_parameter_name = "file";
            string default_content_type = "application/octet-stream";
            NameValueCollection nvc = new NameValueCollection();
            NameValueCollection headers = new NameValueCollection();
            int skip = 0;
            int len = args.Length;
            bool verbose = false;

            while(len>0 && args[skip][0]=='-')
            {
                if (args[skip].Equals("-v"))
                {
                    verbose = true;
                    skip++;
                    len--;
                }
                else if (args[skip].Equals("-i"))
                {
                    if (len < 3)
                    {
                        Console.Error.WriteLine("Error: Missing name and/or value for form item");
                        len = -1;
                        break;
                    }
                    else
                    {
                        skip++;
                        nvc.Add(args[skip++], args[skip++]);
                        len -= 3;
                    }
                }
                else if (args[skip].Equals("-h"))
                {
                    if (len < 3)
                    {
                        Console.Error.WriteLine("Error: Missing name and/or value for header");
                        len = -1;
                        break;
                    }
                    else
                    {
                        skip++;
                        headers.Add(args[skip++], args[skip++]);
                        len -= 3;
                    }
                }
                else
                {
                    Console.Error.WriteLine("Unknown option: {0}", args[skip]);
                    len = -1;
                    break;
                }
            }

            if (len<2)
            {
                Console.Error.WriteLine(
                    "Usage:\n\n\tHTTPFileUploader [options] <url> <file-path> [ <name> [ <content-type> ]]\n\n" +
                        "name defaults to '{0}'\n" +
                        "content-type defaults to '{1}'\n" +
                        "\nOptions:\n" +
                        "\t-v                 - verbose mode\n" +
                        "\t-i <name> <value>  - form item, may be specified multiple times\n" +
                        "\t-h <name> <value>  - headers, may be specified multiple times\n",
                    default_parameter_name,
                    default_content_type
                    );
                Environment.Exit(10);
            }
            string url = args[skip + 0];
            string file_path = args[skip + 1];
            string param_name = len > 2 ? args[skip + 2] : default_parameter_name;
            string content_type = len > 3 ? args[skip + 3] : default_content_type;
            if (verbose)
            {
                Console.Error.WriteLine("URL: '{0}'", url);
                Console.Error.WriteLine("File: '{0}'", file_path);
                Console.Error.WriteLine("Param Name: '{0}'", param_name);
                Console.Error.WriteLine("Content Type: '{0}'", content_type);
                foreach (string key in nvc.Keys)
                {
                    Console.Error.WriteLine("Form Item: '{0}' '{1}'", key, nvc[key]);
                }
                foreach (string key in headers.Keys)
                {
                    Console.Error.WriteLine("Header - {0}: {1}", key, headers[key]);
                }
            }
            Tuple<HttpStatusCode, string> result = HttpUploadFile(url, file_path, param_name, content_type, nvc, headers);
            Console.Error.WriteLine("HTTP Status Code: {0}", result.Item1);
            if (result.Item2.Length > 0) Console.Out.WriteLine(result.Item2);
        }
    }
}
