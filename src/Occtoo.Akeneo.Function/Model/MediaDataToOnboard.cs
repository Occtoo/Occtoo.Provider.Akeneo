using Occtoo.Onboarding.Sdk.Models;
using System.Collections.Generic;

namespace Occtoo.Akeneo.Function.Model
{
    public class MediaDataToOnboard
    {
        public string ProductId { get; set; }

        public Dictionary<string, MediaFamilyData> FamilyData { get; set; }

    }

    public class MediaFamilyData
    {
        public List<DynamicProperty> Properties { get; set; }

        public List<string> Urls { get; set; }
    }
}
