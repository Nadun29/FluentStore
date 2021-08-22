﻿using FluentStore.SDK.Images;
using Flurl;
using Garfoot.Utilities.FluentUrn;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace FluentStore.SDK
{
    public abstract class PackageHandlerBase : IEqualityComparer<PackageHandlerBase>
    {
        public abstract HashSet<string> HandledNamespaces { get; }

        private ImageBase _Image;
        public ImageBase Image
        {
            get
            {
                if (_Image == null)
                    _Image = GetImage();
                return _Image;
            }
        }

        public abstract string DisplayName { get; }

        public abstract ImageBase GetImage();

        public abstract Task<List<PackageBase>> GetFeaturedPackagesAsync();

        public abstract Task<List<PackageBase>> SearchAsync(string query);

        public abstract Task<List<PackageBase>> GetSearchSuggestionsAsync(string query);

        public abstract Task<PackageBase> GetPackage(Urn packageUrn);

        public abstract Task<PackageBase> GetPackageFromUrl(Url url);

        public abstract Url GetUrlFromPackage(PackageBase package);

        public bool Equals(PackageHandlerBase x, PackageHandlerBase y) => x.GetType() == y.GetType();

        public int GetHashCode(PackageHandlerBase obj) => obj.GetType().GetHashCode();
    }
}
