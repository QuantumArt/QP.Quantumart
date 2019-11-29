using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using System.Xml.Linq;

// ReSharper disable once CheckNamespace
namespace Quantumart.QPublishing.Database
{
    // ReSharper disable once InconsistentNaming
    public partial class DBConnector
    {
        private async Task ImportContentDataAsync(XNode dataDoc, CancellationToken cancellationToken)
        {
            var cmd = GetImportContentDataCommand(dataDoc);
            await ProcessDataAsync(cmd, cancellationToken);
        }

        private async Task ReplicateDataAsync(IEnumerable<Dictionary<string, string>> values, int[] attrIds, CancellationToken cancellationToken)
        {
            var cmd = GetReplicateDataCommand(values, attrIds);
            await ProcessDataAsync(cmd);
        }
    }
}
