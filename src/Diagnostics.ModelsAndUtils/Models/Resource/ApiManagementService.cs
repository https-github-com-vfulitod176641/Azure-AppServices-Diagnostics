﻿using Diagnostics.ModelsAndUtils.Attributes;
using Diagnostics.ModelsAndUtils.Models.Storage;
using Diagnostics.ModelsAndUtils.ScriptUtilities;

namespace Diagnostics.ModelsAndUtils.Models
{
    /// <summary>
    /// Resource representing Api Management Service
    /// </summary>
    public class ApiManagementService : ApiManagementServiceFilter, IResource
    {
        /// <summary>
        /// Name of the API Management Service
        /// </summary>
        public string Name { get; set; }

        /// <summary>
        /// Subscription Id(Guid)
        /// </summary>
        public string SubscriptionId { get; set; }

        /// <summary>
        /// Resource Group Name
        /// </summary>
        public string ResourceGroup { get; set; }

        /// <summary>
        /// Resource URI
        /// </summary>
        public string ResourceUri
        {
            get
            {
                return UriUtilities.BuildAzureResourceUri(SubscriptionId, ResourceGroup, Name, Provider, ResourceTypeName);
            }
        }

        /// <summary>
        /// Arm Resource Provider
        /// </summary>
        public string Provider
        {
            get
            {
                return "Microsoft.ApiManagement";
            }
        }

        /// <summary>
        /// Name of Resource Type as defined by ARM resource id. Examples: 'sites', 'hostingEnvironments'
        /// </summary>
        public string ResourceTypeName
        {
            get
            {
                return "service";
            }
        }

        /// <summary>
        /// Subscription Location Placement id
        /// </summary>
        public string SubscriptionLocationPlacementId
        {
            get; set;
        }

        public ApiManagementService(string subscriptionId, string resourceGroup, string name, string subLocationPlacementid = null) : base()
        {
            this.SubscriptionId = subscriptionId;
            this.ResourceGroup = resourceGroup;
            this.Name = name;
            this.SubscriptionLocationPlacementId = subLocationPlacementid;
        }

        /// <summary>
        /// Determines whether the APIM resource is applicable after filtering.
        /// </summary>
        /// <param name="filter">Resource Filter</param>
        /// <returns>True, if resource passes the filter. False otherwise</returns>
        public bool IsApplicable(IResourceFilter filter)
        {
            return filter is ApiManagementServiceFilter;
        }

        /// <summary>
        /// Determines whether the diag entity retrieved from table is applicable after filtering.
        /// </summary>
        /// <param name="diagEntity">Diag Entity from table</param>
        /// <returns>True, if resource passes the filter. False otherwise</returns>
        public bool IsApplicable(DiagEntity diagEntity)
        {
            if(diagEntity == null || diagEntity.ResourceType == null || diagEntity.ResourceProvider == null)
            {
                return false;
            }
            return diagEntity.ResourceProvider == this.Provider && diagEntity.ResourceType == this.ResourceTypeName;
        }
    }
}
