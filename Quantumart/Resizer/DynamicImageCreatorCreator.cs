using Quantumart.QPublishing.FileSystem;
using Quantumart.QPublishing.Info;

namespace Quantumart.QPublishing.Resizer
{
    public class DynamicImageCreatorCreator : IDynamicImageCreator
    {

        private readonly IFileSystem _fs;
        public DynamicImageCreatorCreator(IFileSystem fs)
        {
            _fs = fs;
        }

        public void CreateDynamicImage(DynamicImageInfo image)
        {
            new DynamicImage(image, _fs).Create();
        }
    }
}