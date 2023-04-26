using System;
using Fluid;

namespace Quantumart.QPublishing.Services;

public class FluidBaseMailRenderService : IMailRenderService
{
    public string RenderText(string source, object model)
    {
        FluidParser parser = new();

        if (!parser.TryParse(source, out IFluidTemplate template, out string error))
        {
            throw new InvalidOperationException($"Unable to parse template {source} with error {error}");
        }

        TemplateContext context = new(model);
        return template.Render(context);
    }
}
