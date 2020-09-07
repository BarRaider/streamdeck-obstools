using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace BarRaider.ObsTools.Wrappers
{
    public class SceneCollectionInfo
    {
        [JsonProperty(PropertyName = "name")]
        public String Name { get; set; }
    }
}
