﻿// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.
using System;
using System.Diagnostics;
using System.Net;
using System.Threading.Tasks;
using Microsoft.Owin;
using Microsoft.Owin.FileSystems;
using Microsoft.Owin.StaticFiles;
using Microsoft.Owin.StaticFiles.Infrastructure;
using Newtonsoft.Json.Linq;
using NuGet.Indexing;
using Owin;

namespace NuGet.Services.Search
{
    public class Startup
    {
        private PackageSearcherManager _searcherManager;

        public Func<PackageSearcherManager> SearcherManagerBuilder { get; private set; }
        public string ServiceName = "Search";

        public void Configuration(IAppBuilder app)
        {
            _searcherManager = CreateSearcherManager();

            //test console
            app.Use(async (context, next) =>
            {
                if (String.Equals(context.Request.Path.Value, "/console", StringComparison.OrdinalIgnoreCase))
                {
                    context.Response.Redirect(context.Request.PathBase + context.Request.Path + "/");
                    context.Response.StatusCode = 301;
                    return;
                }
                else if (String.Equals(context.Request.Path.Value, "/console/", StringComparison.OrdinalIgnoreCase))
                {
                    context.Request.Path = new PathString("/console/Index.html");
                }
                await next();
            });

            app.UseStaticFiles(new StaticFileOptions(new SharedOptions
            {
                RequestPath = new PathString("/console"),
                FileSystem = new EmbeddedResourceFileSystem(typeof(Startup).Assembly, "NuGet.Services.Search.Console")
            }));

            app.Run(Invoke);
        }

        public async Task Invoke(IOwinContext context)
        {
            switch (context.Request.Path.Value)
            {
                case "/":
                    JObject response = new JObject();
                    response.Add("name", ServiceName);
                    JObject resources = new JObject();
                    response.Add("resources", resources);
                    resources.Add("range", MakeUri(context, "/range"));
                    resources.Add("fields", MakeUri(context, "/fields"));
                    resources.Add("console", MakeUri(context, "/console"));
                    resources.Add("diagnostics", MakeUri(context, "/diag"));
                    resources.Add("segments", MakeUri(context, "/segments"));
                    resources.Add("query", MakeUri(context, "/query"));
                    context.Response.StatusCode = (int)HttpStatusCode.OK;
                    await context.Response.WriteAsync(response.ToString());
                    break;
                case "/search/query":
                    await QueryMiddleware.Execute(context, _searcherManager);
                    break;
                case "/search/range":
                    await RangeMiddleware.Execute(context, _searcherManager);
                    break;
                case "/search/diag":
                    await DiagMiddleware.Execute(context, _searcherManager);
                    break;
                case "/search/segments":
                    await SegmentsMiddleware.Execute(context, _searcherManager);
                    break;
                case "/search/fields":
                    await FieldsMiddleware.Execute(context, _searcherManager);
                    break;
                default:
                    await context.Response.WriteAsync("unrecognized");
                    context.Response.StatusCode = (int)HttpStatusCode.NotFound;
                    break;
            }
        }

        #region Private Helpers

        private PackageSearcherManager CreateSearcherManager()
        {
            Trace.TraceInformation("InitializeSearcherManager: new PackageSearcherManager");

            var searcher = GetSearcherManager();
            // Ensure the index is initially opened.
            searcher.Open();
            IndexingEventSource.Log.LoadedSearcherManager();
            return searcher;
        }

        private PackageSearcherManager GetSearcherManager()
        {
            if (!String.IsNullOrEmpty(System.Configuration.ConfigurationManager.AppSettings.Get("Search.IndexPath")))
            {
                return PackageSearcherManager.CreateLocal(System.Configuration.ConfigurationManager.AppSettings.Get("Search.IndexPath"));
            }
            else
            {
                return PackageSearcherManager.CreateAzure(
                    System.Configuration.ConfigurationManager.AppSettings.Get("Storage.Primary"),
                    System.Configuration.ConfigurationManager.AppSettings.Get("Search.IndexContainer"),
                    System.Configuration.ConfigurationManager.AppSettings.Get("Search.DataContainer"));
            }
        }

        private string MakeUri(IOwinContext context, string path)
        {
            return new UriBuilder(context.Request.Uri)
            {
                Path = (context.Request.PathBase + new PathString(path)).Value
            }.Uri.AbsoluteUri;
        }

        #endregion Private Helpers
    }
}