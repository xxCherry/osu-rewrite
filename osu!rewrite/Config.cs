using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using SimpleIniConfig;


namespace osu_rewrite
{
    public class Profile
    {
        public string Name { get; set; }
        public string UniqueId { get; set; }
        public string UninstallID { get; set; }
        public string Adapters { get; set; }
    }

    public class RConfig
    {
        private Config config;
        public RConfig() => config = new Config();

        public string Profiles
        {
            get => config.GetValue("Profiles", string.Empty);
            set => config.SetValue("Profiles", value);
        }
        public string SelectedProfile
        {
            get => config.GetValue("SelectedProfile", string.Empty);
            set => config.SetValue("SelectedProfile", value);
        }
        public Profile CurrentProfile
        {  
            get => JsonConvert.DeserializeObject<List<Profile>>(Profiles).Where(x => x.Name == SelectedProfile).FirstOrDefault();
        }
    }
}
