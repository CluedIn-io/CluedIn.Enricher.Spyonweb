// --------------------------------------------------------------------------------------------------------------------
// <copyright file="SpyonwebResponse.cs" company="Clued In">
//   Copyright Clued In
// </copyright>
// <summary>
//   Defines the SpyonwebResponse type.
// </summary>
// --------------------------------------------------------------------------------------------------------------------

using System.Collections.Generic;
using System.Net;

namespace CluedIn.ExternalSearch.Providers.Spyonweb.Models
{
    public class SpyonwebResponse
    {
        public SpyonwebResponse(string clueUri, IPAddress ipAddress, List<string> domains)
        {
            this.ClueUri   = clueUri;
            this.IpAddress = ipAddress;
            this.Domains   = domains;
        }

        public string ClueUri { get; set; }
        public IPAddress IpAddress { get; set; }
        public List<string> Domains { get; set; }
    }
}
