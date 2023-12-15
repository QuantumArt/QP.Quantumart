using Fluid;
using System;

namespace Quantumart.QPublishing.Services;

public class FluidBaseMailRenderService : IMailRenderService
{
    private readonly FluidParser _parser;

    public FluidBaseMailRenderService()
    {
        _parser = new();
    }

    public string RenderText(string source, object model)
    {

        if (!_parser.TryParse(source, out IFluidTemplate template, out string error))
        {
            throw new InvalidOperationException($"Unable to parse template {source} with error {error}");
        }

        TemplateContext context = new(model);
        return template.Render(context);
    }
}
