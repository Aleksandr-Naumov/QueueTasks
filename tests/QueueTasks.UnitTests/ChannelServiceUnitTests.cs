namespace QueueTasks.UnitTests
{
    using QueueTasks.UnitTests.Base;
    using QueueTasks.Services;
    using Xunit;
    using Xunit.Abstractions;
    using FluentAssertions;
    using System.Linq;
    using System.Threading.Tasks;

    public class ChannelServiceUnitTests : BaseUnitTest
    {
        public ChannelServiceUnitTests(ITestOutputHelper output) : base(output)
        {
        }

        [Theory]
        [AutoMoqData]
        public void CreateChannel_Success(ChannelService channelService)
        {
            //arrange
            var operatorId = 1.ToString();

            //act
            var channel = channelService.CreateChannel(operatorId);

            //assert
            var channels = channelService.GetChannels(operatorId);
            channels.Should().NotBeEmpty();
            channels.Should().HaveCount(1);
            channels.Should().Contain(channel);
        }

        [Theory]
        [AutoMoqData]
        public void CreateChannel_CreatedNewCollectionChannels_Success(ChannelService channelService)
        {
            //arrange
            var operatorId = 1.ToString();
            var firstChannel = channelService.CreateChannel(operatorId);
            var firstCollectionChannels = channelService.GetChannels(operatorId);

            //act
            var secondChannel = channelService.CreateChannel(operatorId);
            var secondCollectionChannels = channelService.GetChannels(operatorId);

            //assert
            firstCollectionChannels.Should().NotBeSameAs(secondCollectionChannels);
            firstCollectionChannels.Should().NotBeEmpty();
            secondCollectionChannels.Should().NotBeEmpty();
            firstCollectionChannels.Should().HaveCount(1);
            secondCollectionChannels.Should().HaveCount(2);
            firstCollectionChannels.Should().Contain(firstChannel);
            secondCollectionChannels.Should().Contain(firstChannel);
            secondCollectionChannels.Should().Contain(secondChannel);
        }

        [Theory]
        [AutoMoqData]
        public async Task WriteToChannel_TaskReceived(ChannelService channelService)
        {
            //arrange
            var operatorId = 1.ToString();
            var channel = channelService.CreateChannel(operatorId);

            var (taskId, assigned) = (2.ToString(), true);

            //act
            await channelService.WriteToChannel(taskId, operatorId, assigned);

            //assert
            var channels = channelService.GetChannels(operatorId);
            channels.Should().NotBeEmpty();
            channels.Should().HaveCount(1);
            channels.Should().Contain(channel);

            var taskFromChannel = await channel.Reader.ReadAsync();
            taskFromChannel.TaskId.ShouldBeEqual(taskId);
            taskFromChannel.Assigned.ShouldBeEqual(assigned);
        }
    }
}
