﻿using Newtonsoft.Json;
using System.Collections.Generic;

namespace FluentStoreAPI.Models
{
    public class HomePageFeatured
    {
        [JsonProperty("Carousel")]
        public List<string> Carousel { get; internal set; }
    }
}
