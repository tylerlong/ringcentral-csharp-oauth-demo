﻿using System.Linq;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using RingCentral;
using Newtonsoft.Json;

namespace my_project
{
    public class Startup
    {
        private const string RINGCENTRAL_CLIENT_ID = "<RINGCENTRAL_CLIENT_ID>";
        private const string RINGCENTRAL_CLIENT_SECRET = "<RINGCENTRAL_CLIENT_SECRET>";
        private const string RINGCENTRAL_SERVER_URL = "https://platform.devtest.ringcentral.com";
        private const string RINGCENTRAL_REDIRECT_URL = "http://localhost:5000/oauth2callback";
        private const string SESSION_TOKEN_KEY = "rc-token";

        public void ConfigureServices(IServiceCollection services)
        {
            services.AddMvc().AddSessionStateTempDataProvider();
            services.AddSession();
        }

        private static string Html(string body)
        {
            return $@"<!doctype html><html><body>{body}</body></html>";
        }

        public void Configure(IApplicationBuilder app, IHostingEnvironment env)
        {
            if (env.IsDevelopment()) app.UseDeveloperExceptionPage();
            app.UseSession();
            app.Run(async (context) =>
            {
                var rc = new RestClient(RINGCENTRAL_CLIENT_ID, RINGCENTRAL_CLIENT_SECRET, RINGCENTRAL_SERVER_URL);
                var tokenString = context.Session.GetString(SESSION_TOKEN_KEY);
                if (tokenString != null)
                {
                    rc.token = JsonConvert.DeserializeObject<TokenInfo>(tokenString);
                }
                else if (context.Request.Path != "/oauth2callback")
                {
                    var oauthUri = rc.AuthorizeUri(RINGCENTRAL_REDIRECT_URL);
                    await context.Response.WriteAsync(
                        Html($"<a href=\"{oauthUri}\">Click here to authorize</a>"));
                    return;
                }

                switch (context.Request.Path)
                {
                    case "/":
                        await context.Response.WriteAsync(Html(@"<ul>
                                <li><a href=""/test?api=extension"" target=""_blank"">Read Extension Info</a></li>
                                <li><a href=""/test?api=extension-call-log"" target=""_blank"">Read Extension Call Log</a></li>
                                <li><a href=""/test?api=account-call-log"" target=""_blank"">Read Account Call Log</a></li>
                                <li><a href=""/logout"">Logout</a></li>
                            </ul>"));
                        break;
                    case "/oauth2callback":
                        context.Request.Query.TryGetValue("code", out var codes);
                        var code = codes.First();
                        await rc.Authorize(code, RINGCENTRAL_REDIRECT_URL);
                        context.Session.SetString(SESSION_TOKEN_KEY, JsonConvert.SerializeObject(rc.token));
                        context.Response.Redirect("/");
                        break;
                    case "/test":
                        context.Request.Query.TryGetValue("api", out var apis);
                        var api = apis.First();
                        var result = "";
                        switch (api)
                        {
                            case "extension":
                                result = await rc.Get<string>("/restapi/v1.0/account/~/extension");
                                break;
                            case "extension-call-log":
                                result = await rc.Get<string>("/restapi/v1.0/account/~/extension/~/call-log");
                                break;
                            case "account-call-log":
                                result = await rc.Get<string>("/restapi/v1.0/account/~/call-log");
                                break;
                        }

                        await context.Response.WriteAsync(Html($"<pre>{result}</pre>"));
                        break;
                    case "/logout":
                        await rc.Revoke();
                        context.Session.Remove(SESSION_TOKEN_KEY);
                        context.Response.Redirect("/");
                        break;
                    default:
                        context.Response.StatusCode = 404;
                        break;
                }
            });
        }
    }
}