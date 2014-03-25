using System;
using System.Net;
using System.Text;
using System.Collections.Specialized;
using System.Collections.Generic;
using System.Security.Cryptography;
using System.IO;
using System.Net.Security;
using System.Security.Cryptography.X509Certificates;
 
namespace BTCChinaApiAuthentication {
	class MainClass {
		public static void Main (string[] args) {
			// For https.
			ServicePointManager.ServerCertificateValidationCallback = delegate { return true; };
			 
			// Enter your personal API access key and secret here
			string accessKey = "YOUR_ACCESS_KEY";
			string secretKey = "YOUR_SECRET_KEY";
			string method = "getAccountInfo";
			 
			TimeSpan timeSpan = DateTime.UtcNow - new DateTime (1970, 1, 1);
			long milliSeconds = Convert.ToInt64(timeSpan.TotalMilliseconds * 1000);
			string tonce = Convert.ToString(milliSeconds);
			NameValueCollection parameters = new NameValueCollection() { 
				{ "tonce", tonce },
				{ "accesskey", accessKey },
				{ "requestmethod", "post" },
				{ "id", "1" },
				{ "method", method },
				{ "params", "" }
			};
			string paramsHash = GetHMACSHA1Hash(secretKey, BuildQueryString(parameters));
			string base64String = Convert.ToBase64String(
			Encoding.ASCII.GetBytes(accessKey + ':' + paramsHash));
			string url = "https://api.btcchina.com/api_trade_v1.php";
			string postData = "{\"method\": \"" + method + "\", \"params\": [], \"id\": 1}";
			 
			SendPostByWebRequest(url, base64String, tonce, postData);
		}
		 
		private static void SendPostByWebClient(string url, string base64, string tonce, string postData) {
			using (WebClient client = new WebClient()) {
				client.Headers["Content-type"] = "application/json-rpc";
				client.Headers["Authorization"] = "Basic " + base64;
				client.Headers["Json-Rpc-Tonce"] = tonce;

				try {
					byte[] response = client.UploadData(
					url, "POST", Encoding.Default.GetBytes(postData));
					Console.WriteLine("\nResponse: {0}", Encoding.UTF8.GetString(response));
				} catch (System.Net.WebException ex) {
					Console.WriteLine(ex.Message);
				}
			}
		}
		public static void SendPostByWebRequest(string url, string base64, string tonce, string postData) {
			WebRequest webRequest = WebRequest.Create(url);
			//WebRequest webRequest = HttpWebRequest.Create(url);
			if (webRequest == null) {
				Console.WriteLine("Failed to create web request for url: " + url);
				return;
			}
		 
			byte[] bytes = Encoding.ASCII.GetBytes(postData);
			 
			webRequest.Method = "POST";
			webRequest.ContentType = "application/json-rpc";
			webRequest.ContentLength = bytes.Length;
			webRequest.Headers["Authorization"] = "Basic " + base64;
			webRequest.Headers["Json-Rpc-Tonce"] = tonce;
			try {
				// Send the json authentication post request
				using (Stream dataStream = webRequest.GetRequestStream()) {
					dataStream.Write(bytes, 0, bytes.Length);
					dataStream.Close();
				}

				// Get authentication response
				using (WebResponse response = webRequest.GetResponse()) {
					using (var stream = response.GetResponseStream()) {
						using (var reader = new StreamReader(stream)) {
							Console.WriteLine("Response: " + reader.ReadToEnd());
						}
					}
				}
			} catch (WebException ex) {
				Console.WriteLine(ex.Message);
			}
		}
		 
		private static string BuildQueryString(NameValueCollection parameters) {
			List<string> keyValues = new List<string>();
			foreach (string key in parameters) {
				keyValues.Add(key + "=" + parameters[key]);
			}
			return String.Join("&", keyValues.ToArray());
		}
		 
		private static string GetHMACSHA1Hash(string secret_key, string input) {
			HMACSHA1 hmacsha1 = new HMACSHA1(Encoding.ASCII.GetBytes(secret_key));
			MemoryStream stream = new MemoryStream(Encoding.ASCII.GetBytes(input));
			byte[] hashData = hmacsha1.ComputeHash(stream);
			 
			// Format as hexadecimal string.
			StringBuilder hashBuilder = new StringBuilder();
			foreach (byte data in hashData) {
				hashBuilder.Append(data.ToString("x2"));
			}
			return hashBuilder.ToString();
		}
	}
}
