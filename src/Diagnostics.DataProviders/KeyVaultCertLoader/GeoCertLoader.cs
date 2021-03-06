﻿using System;
using Microsoft.Extensions.Configuration;
using Diagnostics.DataProviders.KeyVaultCertLoader;

namespace Diagnostics.DataProviders
{
    public class GeoCertLoader: CertLoaderBase
    {
        private static readonly Lazy<GeoCertLoader> _instance = new Lazy<GeoCertLoader>(() => new GeoCertLoader());

        public static GeoCertLoader Instance => _instance.Value;

        protected override string Thumbprint { get; set; }
        protected override string SubjectName { get; set; }

        public void Initialize(IConfiguration configuration)
        {
            Thumbprint = configuration["GeoMaster:GeoCertThumbprint"];
            SubjectName = configuration["GeoMaster:GeoCertSubjectName"];
            LoadCertFromAppService();
        }
      
    }
}
