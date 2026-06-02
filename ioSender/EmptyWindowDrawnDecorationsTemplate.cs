using Avalonia.Controls.Chrome;
using Avalonia.Controls.Templates;
using Avalonia.Markup.Xaml.Templates;
using Avalonia.Styling;

namespace ioSender;

public sealed class EmptyWindowDrawnDecorationsTemplate : IWindowDrawnDecorationsTemplate
{
    public TemplateResult<WindowDrawnDecorationsContent> Build() =>
        new(new WindowDrawnDecorationsContent(), null!);

    object ITemplate.Build() => Build().Result;
}
