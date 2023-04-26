namespace Quantumart.QPublishing.Services;

public interface IMailRenderService
{
    string RenderText(string source, object model);
}
