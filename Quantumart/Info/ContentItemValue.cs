using System.Collections.Generic;

namespace Quantumart.QPublishing.Info
{
    public class ContentItemValue
    {
        public string Data { get; set; }

        public AttributeType ItemType { get; set; }

        public HashSet<int> LinkedItems { get; internal set; }

        public ContentItemValue()
        {
            LinkedItems = new HashSet<int>();
        }
    }
}
