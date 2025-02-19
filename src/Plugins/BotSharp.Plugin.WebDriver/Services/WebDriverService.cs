using BotSharp.Abstraction.Agents.Enums;
using BotSharp.Abstraction.MLTasks;
using BotSharp.Core.Infrastructures;

namespace BotSharp.Plugin.WebDriver.Services;

public partial class WebDriverService
{
    private readonly IServiceProvider _services;

    public WebDriverService(IServiceProvider services)
    {
        _services = services;
    }
}
