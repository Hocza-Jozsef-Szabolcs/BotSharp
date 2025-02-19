using BotSharp.Abstraction.Plugins.Models;
using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.Configuration;
using System.Drawing;
using System.IO;
using System.Reflection;
using System.Xml;
using BotSharp.Abstraction.Repositories;

namespace BotSharp.Core.Plugins;

public class PluginLoader
{
    private readonly IServiceCollection _services;
    private readonly IConfiguration _config;
    private readonly PluginSettings _settings;
    private static List<IBotSharpPlugin> _modules = new List<IBotSharpPlugin>();
    private static List<PluginDef> _plugins = new List<PluginDef>();
    private static string _executingDir;

    public PluginLoader(IServiceCollection services,
        IConfiguration config,
        PluginSettings settings)
    {
        _services = services;
        _config = config;
        _settings = settings;
    }

    public void Load(Action<Assembly> loaded)
    {
        _executingDir = Directory.GetParent(Assembly.GetEntryAssembly().Location).FullName;

        _settings.Assemblies.ToList().ForEach(assemblyName =>
        {
            var assemblyPath = Path.Combine(_executingDir, assemblyName + ".dll");
            if (File.Exists(assemblyPath))
            {
                var assembly = Assembly.Load(assemblyName);

                var modules = assembly.GetTypes()
                    .Where(x => x.GetInterface(nameof(IBotSharpPlugin)) != null)
                    .Select(x => Activator.CreateInstance(x) as IBotSharpPlugin)
                    .ToList();

                foreach (var module in modules)
                {
                    module.RegisterDI(_services, _config);
                    // string classSummary = GetSummaryComment(module.GetType());
                    var name = string.IsNullOrEmpty(module.Name) ? module.GetType().Name : module.Name;
                    _modules.Add(module);
                    _plugins.Add(new PluginDef
                    {
                        Id = module.Id,
                        Name = name,
                        Description = module.Description,
                        Assembly = assemblyName,
                        IconUrl = module.IconUrl,
                        AgentIds = module.AgentIds,
                        Menus = module.GetMenus(),
                    });
                    Console.Write($"Loaded plugin ");
                    Console.Write(name, Color.Green);
                    Console.WriteLine($" from {assemblyName}.");
                    if (!string.IsNullOrEmpty(module.Description))
                    {
                        Console.WriteLine(module.Description);
                    }
                }

                loaded(assembly);
            }
            else
            {
                Console.WriteLine($"Can't find assemble {assemblyPath}.");
            }
        });
    }

    public List<PluginDef> GetPlugins(IServiceProvider services)
    {
        // Apply user configurations
        var db = services.GetRequiredService<IBotSharpRepository>();
        var config = db.GetPluginConfig();
        foreach (var plugin in _plugins)
        {
            plugin.Enabled = config.EnabledPlugins.Contains(plugin.Id);
        }
        return _plugins;
    }

    public PluginDef UpdatePluginStatus(IServiceProvider services, string id, bool enable)
    {
        var plugin = _plugins.First(x => x.Id == id);
        plugin.Enabled = enable;

        // save to config
        var db = services.GetRequiredService<IBotSharpRepository>();
        var config = db.GetPluginConfig();
        if (enable)
        {
            if (!config.EnabledPlugins.Exists(x => x == id))
            {
                config.EnabledPlugins.Add(id);
                db.SavePluginConfig(config);
            }

            // enable agents
            var agentService = services.GetRequiredService<IAgentService>();
            foreach (var agentId in plugin.AgentIds) 
            {
                var agent = agentService.LoadAgent(agentId).Result;
                agent.Disabled = false;
                agentService.UpdateAgent(agent, AgentField.Disabled);
            } 
        }
        else
        {
            if (config.EnabledPlugins.Exists(x => x == id))
            {
                config.EnabledPlugins.Remove(id);
                db.SavePluginConfig(config);
            }

            // disable agents
            var agentService = services.GetRequiredService<IAgentService>();
            foreach (var agentId in plugin.AgentIds)
            {
                var agent = agentService.LoadAgent(agentId).Result;
                agent.Disabled = true;
                agentService.UpdateAgent(agent, AgentField.Disabled);
            }
        }
        return plugin;
    }

    public string GetSummaryComment(Type member)
    {
        string summary = string.Empty;
        XmlDocument xmlDoc = new XmlDocument();

        // Load the XML documentation file
        var xmlFile = Path.Combine(_executingDir, $"{member.Module.Assembly.FullName.Split(',')[0]}.xml");
        if (!File.Exists(xmlFile))
        {
            return "";
        }
        xmlDoc.Load(xmlFile); // Replace with your actual XML documentation file name

        // Construct the XPath query to find the summary comment
        string memberName = $"T:{member.FullName}";
        string xpath = $"/doc/members/member[@name='{memberName}']/summary";

        // Find the summary comment using the XPath query
        XmlNode summaryNode = xmlDoc.SelectSingleNode(xpath);

        if (summaryNode != null)
        {
            summary = summaryNode.InnerXml.Trim();
        }

        return summary;
    }

    public void Configure(IApplicationBuilder app)
    {
        if (_modules.Count == 0)
        {
            Console.WriteLine($"No plugin loaded. Please check whether the Load() method is called.", Color.Yellow);
        }

        _modules.ForEach(module =>
        {
            if (module.GetType().GetInterface(nameof(IBotSharpAppPlugin)) != null)
            {
                (module as IBotSharpAppPlugin).Configure(app);
            }
        });
    }
}
