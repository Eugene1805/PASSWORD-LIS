using PASSWORD_LIS_Client.Utils;
using System;
using Xunit;

namespace Test.UtilsTests
{
    public class MessengerTests
    {
        [Fact]
        public void SendUserLoggedOut_WithSubscriber_ShouldInvokeEvent()
        {
            // Arrange
            bool eventInvoked = false;
            Action handler = () => eventInvoked = true;
            Messenger.UserLoggedOut += handler;

            // Act
            Messenger.SendUserLoggedOut();

            // Assert
            Assert.True(eventInvoked);

            // Cleanup
            Messenger.Unsubscribe(handler);
        }

        [Fact]
        public void SendUserLoggedOut_WithMultipleSubscribers_ShouldInvokeAllEvents()
        {
            // Arrange
            int invokeCount = 0;
            Action handler1 = () => invokeCount++;
            Action handler2 = () => invokeCount++;
            Action handler3 = () => invokeCount++;
            
            Messenger.UserLoggedOut += handler1;
            Messenger.UserLoggedOut += handler2;
            Messenger.UserLoggedOut += handler3;

            // Act
            Messenger.SendUserLoggedOut();

            // Assert
            Assert.Equal(3, invokeCount);

            // Cleanup
            Messenger.Unsubscribe(handler1);
            Messenger.Unsubscribe(handler2);
            Messenger.Unsubscribe(handler3);
        }

        [Fact]
        public void Unsubscribe_WithValidHandler_ShouldRemoveSubscription()
        {
            // Arrange
            bool eventInvoked = false;
            Action handler = () => eventInvoked = true;
            Messenger.UserLoggedOut += handler;

            // Act
            Messenger.Unsubscribe(handler);
            Messenger.SendUserLoggedOut();

            // Assert
            Assert.False(eventInvoked);
        }

        [Fact]
        public void Unsubscribe_WithNonExistentHandler_ShouldNotThrowException()
        {
            // Arrange
            Action handler = () => { };

            // Act & Assert
            var exception = Record.Exception(() => Messenger.Unsubscribe(handler));
            Assert.Null(exception);
        }

        [Fact]
        public void Unsubscribe_CalledTwice_ShouldNotThrowException()
        {
            // Arrange
            Action handler = () => { };
            Messenger.UserLoggedOut += handler;

            // Act
            Messenger.Unsubscribe(handler);
            var exception = Record.Exception(() => Messenger.Unsubscribe(handler));

            // Assert
            Assert.Null(exception);
        }

        [Fact]
        public void SendUserLoggedOut_CalledMultipleTimes_ShouldInvokeSubscriberEachTime()
        {
            // Arrange
            int invokeCount = 0;
            Action handler = () => invokeCount++;
            Messenger.UserLoggedOut += handler;

            // Act
            Messenger.SendUserLoggedOut();
            Messenger.SendUserLoggedOut();
            Messenger.SendUserLoggedOut();

            // Assert
            Assert.Equal(3, invokeCount);

            // Cleanup
            Messenger.Unsubscribe(handler);
        }

        [Fact]
        public void Unsubscribe_WithOneOfMultipleHandlers_ShouldOnlyRemoveThatHandler()
        {
            // Arrange
            int invokeCount = 0;
            Action handler1 = () => invokeCount++;
            Action handler2 = () => invokeCount += 10;
            
            Messenger.UserLoggedOut += handler1;
            Messenger.UserLoggedOut += handler2;

            // Act
            Messenger.Unsubscribe(handler1);
            Messenger.SendUserLoggedOut();

            // Assert
            Assert.Equal(10, invokeCount); 

            // Cleanup
            Messenger.Unsubscribe(handler2);
        }

        [Fact]
        public void UserLoggedOut_WhenSubscribed_ShouldAllowResubscription()
        {
            // Arrange
            int invokeCount = 0;
            Action handler = () => invokeCount++;
            
            Messenger.UserLoggedOut += handler;
            Messenger.Unsubscribe(handler);
            Messenger.UserLoggedOut += handler;

            // Act
            Messenger.SendUserLoggedOut();

            // Assert
            Assert.Equal(1, invokeCount);

            // Cleanup
            Messenger.Unsubscribe(handler);
        }
    }
}
