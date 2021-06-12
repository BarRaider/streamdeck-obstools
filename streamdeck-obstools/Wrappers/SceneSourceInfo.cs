using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace BarRaider.ObsTools.Wrappers
{
    public class SceneSourceInfo
    {
        //
        // Summary:
        //     Name of the source
        [JsonProperty(PropertyName = "name")]
        public string Name { get; set; }
    }
}
