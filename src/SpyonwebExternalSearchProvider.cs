// --------------------------------------------------------------------------------------------------------------------
// <copyright file="SpyonwebExternalSearchProvider.cs" company="Clued In">
//   Copyright Clued In
// </copyright>
// <summary>
//   Defines the SpyonwebExternalSearchProvider type.
// </summary>
// --------------------------------------------------------------------------------------------------------------------

using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;

using CluedIn.Core;
using CluedIn.Core.Data;
using CluedIn.Core.Data.Parts;
using CluedIn.ExternalSearch.Filters;
using CluedIn.ExternalSearch.Providers.Spyonweb.Models;

using Newtonsoft.Json.Linq;

using RestSharp;

namespace CluedIn.ExternalSearch.Providers.Spyonweb
{
    /// <summary>The spyonweb external search provider.</summary>
    /// <seealso cref="CluedIn.ExternalSearch.ExternalSearchProviderBase" />
    public class SpyonwebExternalSearchProvider : ExternalSearchProviderBase
    {
        /**********************************************************************************************************
         * CONSTRUCTORS
         **********************************************************************************************************/

        /// <summary>
        /// Initializes a new instance of the <see cref="SpyonwebExternalSearchProvider"/> class.
        /// </summary>
        public SpyonwebExternalSearchProvider()
            : base(Constants.ExternalSearchProviders.SpyonwebId, EntityType.Organization)
        {
        }

        /**********************************************************************************************************
         * METHODS
         **********************************************************************************************************/

        /// <summary>Builds the queries.</summary>
        /// <param name="context">The context.</param>
        /// <param name="request">The request.</param>
        /// <returns>The search queries.</returns>
        public override IEnumerable<IExternalSearchQuery> BuildQueries(ExecutionContext context, IExternalSearchRequest request)
        {
            if (!this.Accepts(request.EntityMetaData.EntityType))
                yield break;

            var existingResults = request.GetQueryResults<SpyonwebResponse>(this).ToList();

            Func<string, bool> nameFilter = value => OrganizationFilters.NameFilter(context, value) || existingResults.Any(r => string.Equals(r.Data.ClueUri.ToString(), value, StringComparison.InvariantCultureIgnoreCase));

            var entityType = request.EntityMetaData.EntityType;
            var organizationWebsite = request.QueryParameters.GetValue(CluedIn.Core.Data.Vocabularies.Vocabularies.CluedInOrganization.Website, new HashSet<string>());

            if (organizationWebsite != null)
            {
                var values = organizationWebsite;

                foreach (var value in values.Where(v => !nameFilter(v)))
                {
                    yield return new ExternalSearchQuery(this, entityType, ExternalSearchQueryParameter.Uri, value);
                }
            }
        }

        /// <summary>Executes the search.</summary>
        /// <param name="context">The context.</param>
        /// <param name="query">The query.</param>
        /// <returns>The results.</returns>
        public override IEnumerable<IExternalSearchQueryResult> ExecuteSearch(ExecutionContext context, IExternalSearchQuery query)
        {
            if (!query.QueryParameters.ContainsKey(ExternalSearchQueryParameter.Uri))
                yield break;

            var uri = query.QueryParameters[ExternalSearchQueryParameter.Uri].FirstOrDefault();

            if (string.IsNullOrEmpty(uri))
                yield break;

            var client = new RestClient("https://api.spyonweb.com/v1");
            var request = new RestRequest(string.Format("domain/{0}", uri), Method.GET);
            request.AddQueryParameter("access_token", "kLl0ynLiDVw2");

            var response = client.ExecuteTaskAsync(request).Result;

            List<SpyonwebResponse> responseModeled = this.GetResultItems(response.Content, query);

            if (response.StatusCode == HttpStatusCode.OK)
            {
                if (response.Content != null)
                {
                    foreach (var spyonwebResponse in responseModeled)
                    {
                        yield return new ExternalSearchQueryResult<SpyonwebResponse>(query, spyonwebResponse);
                    }
                }
            }
            else if (response.StatusCode == HttpStatusCode.NoContent || response.StatusCode == HttpStatusCode.NotFound)
                yield break;
            else if (response.ErrorException != null)
                throw new AggregateException(response.ErrorException.Message, response.ErrorException);
            else
                throw new ApplicationException("Could not execute external search query - StatusCode:" + response.StatusCode + "; Content: " + response.Content);
        }

        /// <summary>Builds the clues.</summary>
        /// <param name="context">The context.</param>
        /// <param name="query">The query.</param>
        /// <param name="result">The result.</param>
        /// <param name="request">The request.</param>
        /// <returns>The clues.</returns>
        public override IEnumerable<Clue> BuildClues(ExecutionContext context, IExternalSearchQuery query, IExternalSearchQueryResult result, IExternalSearchRequest request)
        {
            var resultItem = result.As<SpyonwebResponse>();

            var code = this.GetOriginEntityCode(resultItem, request);

            var clue = new Clue(code, context.Organization);

            this.PopulateMetadata(clue.Data.EntityData, resultItem, request);

            return new[] { clue };
        }

        /// <summary>Gets the primary entity metadata.</summary>
        /// <param name="context">The context.</param>
        /// <param name="result">The result.</param>
        /// <param name="request">The request.</param>
        /// <returns>The primary entity metadata.</returns>
        public override IEntityMetadata GetPrimaryEntityMetadata(ExecutionContext context, IExternalSearchQueryResult result, IExternalSearchRequest request)
        {
            var resultItem = result.As<SpyonwebResponse>();
            return this.CreateMetadata(resultItem, request);
        }

        /// <summary>Gets the preview image.</summary>
        /// <param name="context">The context.</param>
        /// <param name="result">The result.</param>
        /// <param name="request">The request.</param>
        /// <returns>The preview image.</returns>
        public override IPreviewImage GetPrimaryEntityPreviewImage(
            ExecutionContext context,
            IExternalSearchQueryResult result,
            IExternalSearchRequest request)
        {
            return null;
        }

        private IEntityMetadata CreateMetadata(IExternalSearchQueryResult<SpyonwebResponse> resultItem, IExternalSearchRequest request)
        {
            var metadata = new EntityMetadataPart();

            this.PopulateMetadata(metadata, resultItem, request);

            return metadata;
        }

        private EntityCode GetOriginEntityCode(IExternalSearchQueryResult<SpyonwebResponse> resultItem, IExternalSearchRequest request)
        { 
            return new EntityCode(EntityType.Organization, this.GetCodeOrigin(), request.QueryParameters[ExternalSearchQueryParameter.Uri].FirstOrDefault());
        }

        private CodeOrigin GetCodeOrigin()
        {
            return CodeOrigin.CluedIn.CreateSpecific("spyonweb");
        }

        private void PopulateMetadata(IEntityMetadata metadata, IExternalSearchQueryResult<SpyonwebResponse> resultItem, IExternalSearchRequest request)
        {
            var code = this.GetOriginEntityCode(resultItem, request);

            metadata.EntityType         = EntityType.Organization;
            metadata.OriginEntityCode   = code;

            metadata.Codes.Add(code);

            foreach (var domain in resultItem.Data.Domains)
                metadata.Aliases.Add(domain);

            metadata.Aliases.Add(resultItem.Data.IpAddress.ToString());

            metadata.Properties[CluedIn.Core.Data.Vocabularies.Vocabularies.CluedInOrganization.Website] = resultItem.Data.ClueUri;
        }

        private List<SpyonwebResponse> GetResultItems(string response, IExternalSearchQuery query)
        {
            // TODO: Custom json parsing is not need, replace with deserialization to model objects

            var ipAddress = new IPAddress(0);
            var domains = new List<string>();
            var spyonwebResponses = new List<SpyonwebResponse>();

            JObject root, rootResults, ips, ipResults, ipItems;
           
            try
            {
                root = JObject.Parse(response);
            }
            catch
            {
                return new List<SpyonwebResponse>();
            }

            if (root == null)
                return new List<SpyonwebResponse>();

            foreach (var prop in root.Properties())
            {
                if (prop.Name != "result")
                    continue;

                rootResults = JObject.Parse(prop.Value.ToString());

                if (rootResults == null)
                    continue;

                foreach (var result in rootResults.Properties())
                {
                    if (result.Name == "ip")
                    {
                        try
                        {
                            ips = JObject.Parse(result.Value.ToString());
                        }
                        catch
                        {
                            continue;
                        }

                        foreach (JProperty ip in ips.Properties())
                        {
                            if (ip.Name == null)
                                break;

                            if (!IPAddress.TryParse(ip.Name, out ipAddress))
                                continue;

                            ipAddress = IPAddress.Parse(ip.Name);

                            try
                            {
                                ipResults = JObject.Parse(ip.Value.ToString());
                            }
                            catch
                            {
                                continue;
                            }

                            foreach (JProperty ipResult in ipResults.Properties())
                            {
                                if (ipResult.Name == "items")
                                {
                                    try
                                    {
                                        ipItems = JObject.Parse(ipResult.Value.ToString());
                                    }
                                    catch
                                    {
                                        continue;
                                    }

                                    foreach (JProperty ipItem in ipItems.Properties())
                                    {
                                        if (ipItem.Name == null)
                                            break;
                                        var domain = ipItem.Name;
                                        domains.Add(domain);
                                    }
                                }
                            }
                        }

                        spyonwebResponses.Add(new SpyonwebResponse(query.QueryParameters[ExternalSearchQueryParameter.Uri].FirstOrDefault(), ipAddress, domains));
                    }
                }
            }

            return spyonwebResponses;
        }
    }
}