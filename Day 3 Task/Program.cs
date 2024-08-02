using System;
using System.Collections.Generic;
using System.IO;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using Microsoft.Extensions.Configuration;
using Microsoft.Identity.Client;
using Newtonsoft.Json.Linq;

namespace EmployeeMangementSystem
{
    class Program
    {
        #region enums
       
        public class UserDetails
        {
            public string Firstname { get; set; }
            public string Lastname { get; set; }
            public string Email { get; set; }
        }
        #endregion

        private static IConfiguration Configuration { get; set; }

        static async Task Main(string[] args)
        {
            var builder = new ConfigurationBuilder()
                .SetBasePath(AppContext.BaseDirectory)
                .AddJsonFile("appsettings.json", optional: false, reloadOnChange: true);

            Configuration = builder.Build();

            var apiBaseUrl = Configuration["ApiSettings:BaseUrl"];
            var apiKey = Configuration["ApiSettings:ApiKey"];
            var authHeader = Configuration["ApiSettings:AuthorizationHeader"];
            var newDomain = Configuration["EmailSettings:NewDomain"];
            var token = await GetAccessTokenAsync(
                Configuration["AzureAd:ClientId"],
                Configuration["AzureAd:TenantId"],
                Configuration["AzureAd:ClientSecret"],
                Configuration["AzureAd:Authority"]);

            var usersJson = await GetApiResponseAsync(apiBaseUrl, apiKey: apiKey, basicAuthHeader: authHeader);
            var json = JArray.Parse(usersJson);
      

                var users = new List<UserDetails>();
                foreach (var item in json)
                {
                    var email = item["emailAddress"]?.ToString();
                    if (email != null)
                    {
                        email = ChangeEmailDomain(email, newDomain);
                    }
                    string NormaliseSpaces(string input)
                    {
                        string result = Regex.Replace(input, @"\s+", "");

                        return result;
                    }
                    Console.WriteLine(json.LongCount());

                    var user = new UserDetails
                    {
                        Firstname = NormaliseSpaces(item["firstName"]?.ToString()),
                        Lastname = NormaliseSpaces(item["lastName"]?.ToString()),
                        Email = email,
                    };

                    users.Add(user);
                }

                foreach (var user in users)
                {
                    await UpdateUserWithAzureAD(token, user);
                }
            
        }

        private static string ChangeEmailDomain(string email, string newDomain)
        {
            var atIndex = email.IndexOf('@');
            if (atIndex > -1)
            {
                return email.Substring(0, atIndex) + "@" + newDomain;
            }
            return email;
        }

        #region private methods

        private static async Task<string> GetApiResponseAsync(string requestUrl,
            string token = null,
            string apiKey = null,
            string basicAuthHeader = null)
        {
            using (var client = new HttpClient())
            {
                if (!string.IsNullOrEmpty(token))
                {
                    client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);
                }

                if (!string.IsNullOrEmpty(apiKey))
                {
                    client.DefaultRequestHeaders.Add("US-Customer-Api-Key", apiKey);
                }

                if (!string.IsNullOrEmpty(basicAuthHeader))
                {
                    client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Basic", basicAuthHeader);
                }

                try
                {
                    var response = await client.GetAsync(requestUrl);
                    response.EnsureSuccessStatusCode();

                    return await response.Content.ReadAsStringAsync();
                }
                catch (Exception ex)
                {
                    throw new Exception($"Error while making GET request to {requestUrl}", ex);
                }
            }
        }

    private static async Task<string> SendPostRequestAsync(
    string requestUrl,
    string token = null,
    string apiKey = null,
    string basicAuthHeader = null,
    JObject payload = null)
        {
            using (var client = new HttpClient())
            {
                if (!string.IsNullOrEmpty(token))
                {
                    client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);
                }

                if (!string.IsNullOrEmpty(apiKey))
                {
                    client.DefaultRequestHeaders.Add("US-Customer-Api-Key", apiKey);
                }

                if (!string.IsNullOrEmpty(basicAuthHeader))
                {
                    client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Basic", basicAuthHeader);
                }

                var request = new HttpRequestMessage(HttpMethod.Post, requestUrl)
                {
                    Content = new StringContent(payload.ToString(), System.Text.Encoding.UTF8, "application/json")
                };

                try
                {
                    var response = await client.SendAsync(request);
                    response.EnsureSuccessStatusCode();

                    return await response.Content.ReadAsStringAsync();
                }
                catch (Exception ex)
                {
                    throw new Exception($"Error while making POST request to {requestUrl}", ex);
                }
            }
        }

        private static async Task<string> SendPatchRequestAsync(
            string requestUrl,
            string token = null,
            string apiKey = null,
            string basicAuthHeader = null,
            JObject payload = null)
        {
            using (var client = new HttpClient())
            {
                if (!string.IsNullOrEmpty(token))
                {
                    client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);
                }

                if (!string.IsNullOrEmpty(apiKey))
                {
                    client.DefaultRequestHeaders.Add("US-Customer-Api-Key", apiKey);
                }

                if (!string.IsNullOrEmpty(basicAuthHeader))
                {
                    client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Basic", basicAuthHeader);
                }

                var request = new HttpRequestMessage(new HttpMethod("PATCH"), requestUrl)
                {
                    Content = new StringContent(payload.ToString(), System.Text.Encoding.UTF8, "application/json")
                };

                try
                {
                    var response = await client.SendAsync(request);
                    response.EnsureSuccessStatusCode();

                    return await response.Content.ReadAsStringAsync();
                }
                catch (Exception ex)
                {
                    throw new Exception($"Error while making PATCH request to {requestUrl}", ex);
                }
            }
        }


        private static async Task<string> GetAccessTokenAsync(string clientId, string tenantId, string clientSecret, string authority)
        {
            try
            {
                var confidentialClient = ConfidentialClientApplicationBuilder
                    .Create(clientId)
                    .WithTenantId(tenantId)
                    .WithClientSecret(clientSecret)
                    .WithAuthority(new Uri(authority))
                    .Build();

                var result = await confidentialClient
                    .AcquireTokenForClient(new[] { "https://graph.microsoft.com/.default" })
                    .ExecuteAsync();

                return result.AccessToken;
            }
            catch (Exception ex)
            {
                throw new Exception("Error acquiring access token", ex);
            }
        }

        private static async Task UpdateUserWithAzureAD(string token, UserDetails user)
        {

            var requestUrl = $"https://graph.microsoft.com/v1.0/users?$filter=mail eq '{user.Email}'";
            var userJson = await GetApiResponseAsync(requestUrl, token: token);

            var json = JObject.Parse(userJson);

              if (json["value"].HasValues)
                    {
                        var userId = json["value"][0]["id"].ToString();

                        var currentDetails = json["value"][0];
                        var currentFirstname = currentDetails["givenName"]?.ToString();
                        var currentLastname = currentDetails["surname"]?.ToString();
                        var currentEmail = currentDetails["mail"]?.ToString();

                        var changes = new List<string>();

                        if (currentFirstname != user.Firstname)
                        {
                            changes.Add($"First name changed from '{currentFirstname}' to '{user.Firstname}'");
                        }

                        if (currentLastname != user.Lastname)
                        {
                            changes.Add($"Last name changed from '{currentLastname}' to '{user.Lastname}'");
                        }

                        if (currentEmail != user.Email)
                        {
                            changes.Add($"Email changed from '{currentEmail}' to '{user.Email}'");
                        }

                        

                        await UpdateUser(token, userId, user);
                        await LogAudit(Configuration["AuditLogPath"], user.Email, "Success", "User updated successfully");

                        if (changes.Count > 0)
                        {
                            var changesLog = string.Join("; ", changes);
                            await LogAudit(Configuration["AuditLogPath"], user.Email, "Success", $"User updated: {changesLog}");
                        }
                    }
                    else
                    {
                        await CreateUser(token, user);
                        await LogAudit(Configuration["AuditLogPath"], user.Email, "Success", "User created successfully");
                    }
                

            
        }

        private static async Task CreateUser(string token, UserDetails user)
        {
            try
            {
                
                    var requestUrl = "https://graph.microsoft.com/v1.0/users";

                    var userJson = new JObject
                    {
                        {"accountEnabled", true},
                        {"displayName", $"{user.Firstname} {user.Lastname}"},
                        {"mailNickname", user.Firstname.ToLower()},
                        {"userPrincipalName", user.Email},
                        {"mail", user.Email},
                        {"givenName", user.Firstname},
                        {"surname", user.Lastname},
                        {"jobTitle", "Associate Technical Consultant"},
                        {"passwordProfile", new JObject
                            {
                                {"forceChangePasswordNextSignIn", true},
                                {"password", "TempPassword123!"}
                            }
                        }
                    };
                await SendPostRequestAsync(requestUrl, token, payload: userJson);                
            }
            catch (Exception ex)
            {
                throw new Exception($"Error while creating user", ex);
            }
        }

        private static async Task UpdateUser(string token, string userId, UserDetails user)
        {
            try
            {
                
                    var requestUrl = $"https://graph.microsoft.com/v1.0/users/{userId}";

                    var userJson = new JObject
                    {
                        
                        {"displayName", $"{user.Firstname} {user.Lastname}"},
                        {"givenName", user.Firstname},
                        {"surname", user.Lastname},
                        {"mail", user.Email},
                    };

        await SendPatchRequestAsync(requestUrl, token, payload: userJson);
                
            }
            catch (Exception ex)
            {
                throw new Exception("Error while updating user", ex);
            }
        }

        private static async Task LogAudit(string logFilePath, string email, string type, string message)
        {
            var logMessage = $"{DateTime.UtcNow}: {type} - {email} - {message}{Environment.NewLine}";
            await File.AppendAllTextAsync(logFilePath, logMessage);
        }

        #endregion
    }
}
