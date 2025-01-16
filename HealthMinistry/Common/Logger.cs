using RestSharp;
using System;
using System.Collections.Generic;
using System.Configuration;
using System.IO;
using System.Linq;
using System.Net;
using System.Text;
using System.Web;

namespace HealthMinistry.Common
{
    public static class Logger
    {
        private static string LOG  = ConfigurationManager.AppSettings["LOG"];

       
        public static void Write(string s)
        {

            try
            {
                string fullPath = Path.Combine(LOG, "Nautilus_WS" + "-" + DateTime.Now.ToString("dd-MM-yyyy") + ".txt");

                using (FileStream stream = new FileStream(fullPath, FileMode.Append, FileAccess.Write))
                {
                    StreamWriter streamWriter = new StreamWriter(stream);
                    streamWriter.WriteLine(DateTime.Now.ToString("MM/dd/yyyy hh:mm:ss.fff"));
                    streamWriter.WriteLine(s);
                    streamWriter.WriteLine();
                    streamWriter.Close();
                }
            }
            catch
            {
            }

        }
        public static void LogReuest(HttpWebRequest request)
        {

            Write(request.RequestUri.ToString());         // The URI of the request
                                                          //    Write(request.Address             );// The endpoint being accessed
            Write(request.Host);// The host name/authority
            Write(request.ConnectionGroupName);// Name of connection group (useful for logging groups of requests)

            ;// Headers that are useful for logging
             //Write(request.Headers .Coun          );// All headers collection
            Write(request.ContentType);// Content type being sent
                                       // Write(request.ContentLength    );// Size of the request in bytes
            Write(request.UserAgent);// User agent string
            Write(request.Accept);// Accept header value

            // Timing/Performance properties
            Write(request.Timeout.ToString());// Request timeout value
            Write(request.ReadWriteTimeout.ToString());// Read/write timeout value
            Write(request.ContinueTimeout.ToString());// 100-continue timeout value

            // Connection info
            Write(request.ProtocolVersion.ToString());// HTTP protocol version
            Write(request.ServicePoint.ToString());// Details about the connection to the server
            Write(request.Proxy.ToString());
        }
        public static void LogRequestParameters(RestRequest request, string outputPath)
        {
            var parameters = new StringBuilder();
            parameters.AppendLine("REST Request Parameters Log");
            parameters.AppendLine("------------------------");
            parameters.AppendLine($"Method: {request.Method}");
            parameters.AppendLine($"Resource: {request.Resource}");
            parameters.AppendLine($"MultipartFormData: {request.AlwaysMultipartFormData}");

            // Headers
            parameters.AppendLine("\nHeaders:");
            foreach (var header in request.Parameters.Where(p => p.Type == ParameterType.HttpHeader))
            {
                parameters.AppendLine($"- {header.Name}: {header.Value}");
            }

            // Files
            parameters.AppendLine("\nFiles:");
            foreach (var file in request.Files)
            {
                parameters.AppendLine($"- Name: {file.Name}");
                parameters.AppendLine($"  Path: {file.FileName}");
                parameters.AppendLine($"  Content Type: {file.ContentType}");
            }

            // Other Parameters
            parameters.AppendLine("\nOther Parameters:");
            foreach (var param in request.Parameters.Where(p => p.Type != ParameterType.HttpHeader))
            {
                parameters.AppendLine($"- Type: {param.Type}");
                parameters.AppendLine($"  Name: {param.Name}");
                parameters.AppendLine($"  Value: {param.Value}");
            }

            try
            {
                System.IO.File.WriteAllText(outputPath, parameters.ToString());


            }
            catch (Exception ex)
            {
                throw new Exception($"Failed to write parameters to file: {ex.Message}", ex);
            }
        }
        public static void LogResponse(IRestResponse response, string outputPath)
        {
            var log = new StringBuilder();
            log.AppendLine("REST Response Log");
            log.AppendLine("----------------");
            log.AppendLine($"Status Code: {(int)response.StatusCode} ({response.StatusCode})");
            log.AppendLine($"Status Description: {response.StatusDescription}");
            log.AppendLine($"Content Length: {response.ContentLength}");
            log.AppendLine($"Response Status: {response.ResponseStatus}");
            // log.AppendLine($"Execution Time: {response.ExecutionTime}ms");

            // Response Headers
            log.AppendLine("\nResponse Headers:");
            foreach (var header in response.Headers)
            {
                log.AppendLine($"- {header.Name}: {header.Value}");
            }

            // Cookies if any
            if (response.Cookies.Count > 0)
            {
                log.AppendLine("\nCookies:");
                foreach (var cookie in response.Cookies)
                {
                    log.AppendLine($"- {cookie.Name}: {cookie.Value}");
                }
            }

            // Error Details
            if (response.ErrorMessage != null)
            {
                log.AppendLine("\nError Details:");
                log.AppendLine($"Error Message: {response.ErrorMessage}");
                if (response.ErrorException != null)
                {
                    log.AppendLine($"Exception: {response.ErrorException}");
                }
            }

            // Response Content
            log.AppendLine("\nResponse Content:");
            try
            {

                log.AppendLine(response.Content);

            }
            catch
            {
                // If JSON formatting fails, write raw content
                log.AppendLine(response.Content);
            }
            try
            {
                System.IO.File.WriteAllText(outputPath, log.ToString());


            }
            catch (Exception ex)
            {
                throw new Exception($"Failed to write response log to file: {ex.Message}", ex);
            }
        }
    }
}