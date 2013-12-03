using System;
using System.Collections.Generic;
using System.IO;
using System.Net;
using System.Net.Http;
using System.Threading.Tasks;
using Microsoft.Phone.Tasks;
using Newtonsoft.Json;

namespace WindowsAzure.Acs.Oauth2.Client.WinRT.Protocol
{
    public class OAuthMessageSerializer
    {
        public async Task<OAuthMessage> Read(HttpResponseMessage response)
        {
            if (response == null)
            {
                throw new ArgumentNullException("response");
            }

            var responseStream = await response.Content.ReadAsStreamAsync();
            return this.Read(
                response.Content.Headers.ContentType != null ? response.RequestMessage.Method.ToString() : "GET", 
                response.Content.Headers.ContentType != null ? response.Content.Headers.ContentType.MediaType : null
                , response.RequestMessage.RequestUri, responseStream);
        }

        public virtual OAuthMessage Read(string httpMethod, string httpContentType, Uri requestUri, System.IO.Stream incomingStream)
        {
            if (string.IsNullOrEmpty(httpMethod))
            {
                throw new ArgumentOutOfRangeException("httpMethod");
            }
            if (requestUri == null)
            {
                throw new ArgumentNullException("requestUri");
            }
            if (incomingStream == null)
            {
                throw new ArgumentNullException("incomingStream");
            }

            Dictionary<string,string> oAuthParameters = new Dictionary<string,string>();
            if (httpMethod == "POST")
            {
                if (httpContentType.Contains("application/x-www-form-urlencoded"))
                {
                    oAuthParameters = this.ReadFormEncodedParameters(incomingStream);
                }
                else
                {
                    if (!httpContentType.Contains("application/json"))
                    {
                        throw new OAuthMessageSerializationException("");
                    }
                    oAuthParameters = this.ReadJsonEncodedParameters(incomingStream);
                }
            }
            else
            {
                if (!(httpMethod == "GET"))
                {
                    throw new OAuthMessageSerializationException("");
                }
                oAuthParameters = HttpQueryStringParser.Parse(requestUri.Query);
            }
            return this.CreateTypedOAuthMessageFromParameters(OAuthMessageSerializer.GetBaseUrl(requestUri), oAuthParameters);
        }

        public virtual Dictionary<string,string> ReadFormEncodedParameters(System.IO.Stream incomingStream)
        {
            if (incomingStream == null)
            {
                throw new ArgumentNullException("incomingStream");
            }
            System.IO.StreamReader reader = new System.IO.StreamReader(incomingStream);
            return HttpQueryStringParser.Parse(reader.ReadToEnd());
        }

        public virtual Dictionary<string,string> ReadJsonEncodedParameters(System.IO.Stream incomingStream)
        {
            if (incomingStream == null)
            {
                throw new ArgumentNullException("incomingStream");
            }

            Dictionary<string,string> parameters = new Dictionary<string,string>();
            var jsonReader = new JsonTextReader(new StreamReader(incomingStream));

            while (jsonReader.Read())
            {
                // Not interested in nested objects/arrays! 
                if (jsonReader.Depth > 1)
                {
                    jsonReader.Skip();
                }
                else if (jsonReader.TokenType == JsonToken.PropertyName)
                {
                    string key = jsonReader.Value.ToString();
                    if (jsonReader.Read())
                    {
                        switch (jsonReader.TokenType)
                        {
                            case JsonToken.Boolean:
                            case JsonToken.Date:
                            case JsonToken.Float:
                            case JsonToken.Integer:
                            case JsonToken.Null:
                            case JsonToken.String:
                                parameters[key] = jsonReader.Value.ToString();
                                break;
                        }
                    }
                }
            }

            return parameters;
        }

        public virtual ResourceAccessFailureResponse ReadAuthenticationHeader(string authenticateHeader, Uri resourceUri)
        {
            if (string.IsNullOrEmpty(authenticateHeader))
            {
                throw new ArgumentNullException("authenticateHeader");
            }
            if (resourceUri == null)
            {
                throw new ArgumentNullException("resourceUri");
            }

            ResourceAccessFailureResponse response = null;
            string expectedAuthType = "Bearer";
            string authType = authenticateHeader.Split(new char[] { ' ' }, 2)[0];

            if (string.IsNullOrEmpty(authType))
            {
                throw new OAuthMessageSerializationException("");
            }

            Dictionary<string,string> keyValuePairs = new Dictionary<string,string>();
            if (authType.Contains(expectedAuthType))
            {
                response = new ResourceAccessFailureResponse(resourceUri);
                authenticateHeader = authenticateHeader.Remove(0, authType.Length);
                authenticateHeader = authenticateHeader.TrimStart(new char[] { ' ' });
                if (!string.IsNullOrEmpty(authenticateHeader))
                {
                    string[] parameters = authenticateHeader.Split(new string[] { "\", " }, System.StringSplitOptions.None);
                    for (int i = 0; i < parameters.Length; i++)
                    {
                        string entry = parameters[i];
                        string splitAtEqualSign = "=\"";
                        string[] pairs = entry.Split(new string[] { splitAtEqualSign }, 2, System.StringSplitOptions.None);
                        if (pairs.Length != 2)
                        {
                            throw new OAuthMessageSerializationException("");
                        }
                        if (i == parameters.Length - 1 && pairs[1][pairs[1].Length - 1] == '"')
                        {
                            pairs[1] = pairs[1].Remove(pairs[1].Length - 1, 1);
                        }
                        keyValuePairs.Add(pairs[0], pairs[1]);
                    }
                    foreach (var parameter in keyValuePairs)
                    {
                        response.Parameters.Add(parameter.Key, parameter.Value);
                    }
                    response.Validate();
                }
            }

            return response;
        }

        protected virtual OAuthMessage CreateTypedOAuthMessageFromParameters(Uri baseUri, Dictionary<string,string> parameters)
        {
            if (parameters == null)
            {
                throw new ArgumentNullException("parameters");
            }

            OAuthMessage request = null;
            if (parameters.ContainsKey("response_type") && (parameters["response_type"] == "code" || parameters["response_type"] == "token"))
            {
                request = new EndUserAuthorizationRequest(baseUri);
            }
            if ((parameters.ContainsKey("code")  && !string.IsNullOrEmpty(parameters["code"])) || (parameters.ContainsKey("access_token") && !string.IsNullOrEmpty(parameters["access_token"]) && parameters.ContainsKey("refresh_token")  && string.IsNullOrEmpty(parameters["refresh_token"])))
            {
                request = new EndUserAuthorizationResponse(baseUri);
            }
            if (parameters.ContainsKey("error")  && !string.IsNullOrEmpty(parameters["error"]))
            {
                request = new EndUserAuthorizationFailedResponse(baseUri);
            }
            if (parameters.ContainsKey("grant_type")  && !string.IsNullOrEmpty(parameters["grant_type"]) && parameters["grant_type"] == "authorization_code")
            {
                request = new AccessTokenRequestWithAuthorizationCode(baseUri);
            }
            if (parameters.ContainsKey("access_token")  && !string.IsNullOrEmpty(parameters["access_token"]))
            {
                request = new AccessTokenResponse(baseUri);
            }
            if (request == null)
            {
                throw new OAuthMessageSerializationException("");
            }

            foreach (var parameter in parameters)
            {
                request.Parameters.Add(parameter.Key, parameter.Value);
            }
            request.Validate();
            return request;
        }

        public async virtual void Write(OAuthMessage message, HttpWebRequest request)
        {
            if (message == null)
            {
                throw new ArgumentNullException("message");
            }
            if (request == null)
            {
                throw new ArgumentNullException("request");
            }

            request.Method = this.GetHttpMethod(message);
            request.ContentType = this.GetHttpContentType(message);

            var t = Task.Factory.FromAsync<System.IO.Stream>(
                            request.BeginGetRequestStream(null, null),
                            request.EndGetRequestStream);

            this.Write(message, await t  );
        }

        public virtual void Write(OAuthMessage message, System.IO.Stream requestStream)
        {
            if (message == null)
            {
                throw new ArgumentNullException("message");
            }
            if (requestStream == null)
            {
                throw new ArgumentNullException("requestStream");
            }

            System.IO.StreamWriter streamWriter = new System.IO.StreamWriter(requestStream);
            AccessTokenRequest atRequestMsg = message as AccessTokenRequest;
            if (atRequestMsg != null)
            {
                streamWriter.Write(this.GetFormEncodedQueryFormat(message));
                streamWriter.Flush();
                return;
            }

            AccessTokenResponse atResponseMsg = message as AccessTokenResponse;
            if (atResponseMsg != null)
            {
                streamWriter.Write(this.GetJsonEncodedFormat(message));
                streamWriter.Flush();
                return;
            }

            throw new OAuthMessageException("");
        }

        public virtual string GetQueryStringFormat(OAuthMessage message)
        {
            if (message == null)
            {
                throw new ArgumentNullException("message");
            }

            System.Text.StringBuilder strBuilder = new System.Text.StringBuilder();
            strBuilder.Append(message.BaseUri.AbsoluteUri);
            strBuilder.Append("?");
            strBuilder.Append(this.GetFormEncodedQueryFormat(message));
            return strBuilder.ToString();
        }

        public virtual string GetFormEncodedQueryFormat(OAuthMessage message)
        {
            if (message == null)
            {
                throw new ArgumentNullException("message");
            }
            System.Text.StringBuilder strBuilder = new System.Text.StringBuilder();
            bool skipDelimiter = true;
            foreach (string key in message.Parameters.Keys)
            {
                if (message.Parameters[key] != null)
                {
                    if (!skipDelimiter)
                    {
                        strBuilder.Append("&");
                    }
                    strBuilder.Append(string.Format(System.Globalization.CultureInfo.InvariantCulture, "{0}={1}", new object[]
                                                                                                                      {
                                                                                                                          key, 
                                                                                                                          WebUtility.UrlEncode(message.Parameters[key])
                                                                                                                      }));
                    skipDelimiter = false;
                }
            }
            return strBuilder.ToString();
        }

        public virtual string GetJsonEncodedFormat(OAuthMessage message)
        {
            if (message == null)
            {
                throw new ArgumentNullException("message");
            }

            var serializedMessage = JsonConvert.SerializeObject(message.Parameters);

            // TODO: replace token of array to object...
            return serializedMessage;
        }

        public virtual string GetHttpMethod(OAuthMessage message)
        {
            if (message == null)
            {
                throw new ArgumentNullException("message");
            }

            AccessTokenRequest atRequestMessage = message as AccessTokenRequest;
            if (atRequestMessage != null)
            {
                return "POST";
            }

            AccessTokenResponse atResponseMessage = message as AccessTokenResponse;
            if (atResponseMessage != null)
            {
                return "POST";
            }

            return "GET";
        }

        public virtual string GetHttpContentType(OAuthMessage message)
        {
            if (message == null)
            {
                throw new ArgumentNullException("message");
            }

            AccessTokenRequest atRequestMessage = message as AccessTokenRequest;
            if (atRequestMessage != null)
            {
                return "application/x-www-form-urlencoded";
            }

            AccessTokenResponse atResponseMessage = message as AccessTokenResponse;
            if (atResponseMessage != null)
            {
                return "application/json";
            }
            return "text/plain; charset=us-ascii";
        }

        private static Uri GetBaseUrl(Uri uri)
        {
            if (uri == null)
            {
                throw new ArgumentNullException("uri");
            }

            string tempUri = uri.AbsoluteUri;
            int index = tempUri.IndexOf("?", 0, System.StringComparison.Ordinal);
            if (index > -1)
            {
                tempUri = tempUri.Substring(0, index);
            }
            return new Uri(tempUri);
        }
    }
}