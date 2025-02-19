using BotSharp.Abstraction.Agents;
using BotSharp.Abstraction.Conversations.Models;
using Moq;
using BotSharp.Abstraction.Agents.Enums;
using BotSharp.Abstraction.Agents.Models;
using BotSharp.Abstraction.Conversations;
using Microsoft.SemanticKernel.AI.ChatCompletion;
using Microsoft.SemanticKernel.AI;
using BotSharp.Plugin.SemanticKernel.UnitTests.Helpers;
using BotSharp.Abstraction.Loggers;

namespace BotSharp.Plugin.SemanticKernel.Tests
{
    public class SemanticKernelChatCompletionProviderTests
    {
        private readonly Mock<IChatCompletion> _chatCompletionMock;
        private readonly Mock<IServiceProvider> _servicesMock;
        private readonly Mock<ITokenStatistics> _tokenStatisticsMock;
        private readonly SemanticKernelChatCompletionProvider _provider;

        public SemanticKernelChatCompletionProviderTests()
        {
            _chatCompletionMock = new Mock<IChatCompletion>();
            _servicesMock = new Mock<IServiceProvider>();
            _tokenStatisticsMock = new Mock<ITokenStatistics>();
            _provider = new SemanticKernelChatCompletionProvider(_chatCompletionMock.Object, _servicesMock.Object, _tokenStatisticsMock.Object);
        }

        [Fact]
        public async void GetChatCompletions_Returns_RoleDialogModel()
        {
            // Arrange
            var agent = new Agent();
            var conversations = new List<RoleDialogModel>
            {
                new RoleDialogModel(AgentRole.User, "Hello")
            };

            _servicesMock.Setup(x => x.GetService(typeof(IEnumerable<IContentGeneratingHook>)))
                        .Returns(new List<IContentGeneratingHook>());
            var agentService = new Mock<IAgentService>();
            agentService.Setup(x => x.RenderedInstruction(agent)).Returns("");
            _servicesMock.Setup(x => x.GetService(typeof(IAgentService)))
                .Returns(agentService.Object);

            var chatHistoryMock = new Mock<ChatHistory>();
            _chatCompletionMock.Setup(x => x.CreateNewChat(It.IsAny<string>())).Returns(chatHistoryMock.Object);
            _chatCompletionMock.Setup(x => x.GetChatCompletionsAsync(chatHistoryMock.Object, It.IsAny<AIRequestSettings>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(new List<IChatResult>
            {
                new ResultHelper("How can I help you?")
            });


            // Act
            var result = await _provider.GetChatCompletions(agent, conversations);

            // Assert
            Assert.IsType<RoleDialogModel>(result);
        }
    }

}
