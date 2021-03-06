﻿using Jamstack.On.Dotnet.Models;
using Jamstack.On.Dotnet.ViewModels;
using Kentico.Kontent.Delivery.Abstractions;
using Kentico.Kontent.Delivery.Urls.QueryParameters;
using Kentico.Kontent.Delivery.Urls.QueryParameters.Filters;
using Kontent.Statiq;
using Statiq.Common;
using Statiq.Core;
using Statiq.Razor;
using System;
using System.Linq;

namespace Jamstack.On.Dotnet.Pipelines
{
    public class RootPipeline : Pipeline
    {
        private const string URL_PATH_KEY = "Jamstack.On.Dotnet.Pipelines.RootPipeline.UrlPath";
        private Root rootNode;

        public RootPipeline(IDeliveryClient client)
        {
            InputModules = new ModuleList
            {
                new Kontent<Root>(client)
                    .WithQuery(
                        new EqualsFilter("system.codename", "root"),
                        new LimitParameter(1),
                        new DepthParameter(3)
                    ),
                new ReplaceDocuments(
                    KontentConfig.GetChildren<Root>(root =>
                    {          
                        // For multiple levels of menu it is neccesary to flatten the Page structure and prepare metadata from the parent information
                        rootNode = root;
                        return root.Subpages.OfType<Page>();
                    })
                ),

                // option
                new ForEachDocument(
                    // Hack to get information from parent - it would be great to have info about the prarent (somehow)
                    new SetMetadata(URL_PATH_KEY, Config.FromDocument((doc, ctx) =>
                    {
                        return doc.AsKontent<Page>().Url;
                    })),
                    new MergeDocuments(
                        KontentConfig.GetChildren<Page>(page =>
                        {
                            return page.Content;
                        })
                    )
                )
            };

            ProcessModules = new ModuleList {
                new ForEachDocument(
                    new MergeContent(
                        new ReadFiles(
                            Config.FromDocument((document, context) =>
                            {
                                var typeCodename = document
                                    .FilterMetadata(KontentKeys.System.Type)
                                    .Values
                                    ?.FirstOrDefault()
                                    ?.ToString();

                                switch (typeCodename)
                                {
                                    case LandingPage.Codename:
                                        return "_partials/LandingPage.cshtml";
                                    default:
                                        throw new NotImplementedException($"Template not implemented for page content type {typeCodename}");
                                }
                            })
                        )
                    ),
                    new RenderRazor()
                        .WithModel(Config.FromDocument((document, context) =>
                        {
                            var typeCodename = document
                                .FilterMetadata(KontentKeys.System.Type)
                                .Values
                                ?.FirstOrDefault()
                                ?.ToString();

                            System.Diagnostics.Debug.WriteLine(rootNode.Keywords);

                            switch (typeCodename)
                            {
                                case LandingPage.Codename:
                                    return new LandingPageWithMetadata
                                    {
                                        Metadata = rootNode,
                                        LandingPage = document.AsKontent<LandingPage>()
                                    };
                                default:
                                    throw new NotImplementedException($"Template not implemented for page content type {typeCodename}");
                            }
                        })),
                    new SetDestination(Config.FromDocument((doc, ctx) => {
                        var url = doc.FilterMetadata(URL_PATH_KEY).FirstOrDefault().Value as string;
                        if(String.IsNullOrEmpty(url))
                        {
                            throw new ApplicationException($"Problem with Url of the page document - it is not set");
                        } else if(url == "/")
                        {
                            return new NormalizedPath("index.html");
                        }

                        return new NormalizedPath($"{url}.html");

                    }))
                )
            };

            OutputModules = new ModuleList {
                new WriteFiles()
            };
        }
    }
}
