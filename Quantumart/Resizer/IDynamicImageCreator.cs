using Quantumart.QPublishing.Info;

// ReSharper disable once CheckNamespace
namespace Quantumart.QPublishing.Resizer
{
    public interface IDynamicImageCreator
    {
        void CreateDynamicImage(DynamicImageInfo image);
    }
}
