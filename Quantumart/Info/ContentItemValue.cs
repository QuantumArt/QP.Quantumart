using System.Collections.Generic;

namespace Quantumart.QPublishing.Info
{
    public class ContentItemValue
    {
        public string Data { get; set; }

        internal AttributeType ItemType { get; set; }

        internal bool IsClassifier { get; set; }

        internal int ClassifierBaseArticle { get; set; }

        public HashSet<int> LinkedItems { get; internal set; }

        public ContentItemValue()
        {
            LinkedItems = new HashSet<int>();
        }
    }
}
