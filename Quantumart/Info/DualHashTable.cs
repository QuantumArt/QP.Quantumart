using System.Collections;

// ReSharper disable once CheckNamespace
namespace Quantumart.QPublishing.Info
{
    public class DualHashTable
    {
        public DualHashTable()
        {
            Ids = new Hashtable();
            Items = new Hashtable();
        }

        public Hashtable Ids { get; }

        public Hashtable Items { get; }
    }
}
