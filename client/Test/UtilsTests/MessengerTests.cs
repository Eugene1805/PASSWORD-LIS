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
            bool eventInvoked = false;
            Action handler = () => eventInvoked = true;
            Messenger.UserLoggedOut += handler;
            Messenger.SendUserLoggedOut();
            Assert.True(eventInvoked);
            Messenger.Unsubscribe(handler);
        }

        [Fact]
        public void SendUserLoggedOut_WithMultipleSubscribers_ShouldInvokeAllEvents()
        {
            int invokeCount = 0;
            Action handler1 = () => invokeCount++;
            Action handler2 = () => invokeCount++;
            Action handler3 = () => invokeCount++;
            Messenger.UserLoggedOut += handler1;
            Messenger.UserLoggedOut += handler2;
            Messenger.UserLoggedOut += handler3;
            Messenger.SendUserLoggedOut();
            Assert.Equal(3, invokeCount);
            Messenger.Unsubscribe(handler1);
            Messenger.Unsubscribe(handler2);
            Messenger.Unsubscribe(handler3);
        }

        [Fact]
        public void Unsubscribe_WithValidHandler_ShouldRemoveSubscription()
        {
            bool eventInvoked = false;
            Action handler = () => eventInvoked = true;
            Messenger.UserLoggedOut += handler;
            Messenger.Unsubscribe(handler);
            Messenger.SendUserLoggedOut();
            Assert.False(eventInvoked);
        }

        [Fact]
        public void Unsubscribe_WithNonExistentHandler_ShouldNotThrowException()
        {
            Action handler = () => { };
            var exception = Record.Exception(() => Messenger.Unsubscribe(handler));
            Assert.Null(exception);
        }

        [Fact]
        public void Unsubscribe_CalledTwice_ShouldNotThrowException()
        {
            Action handler = () => { };
            Messenger.UserLoggedOut += handler;
            Messenger.Unsubscribe(handler);
            var exception = Record.Exception(() => Messenger.Unsubscribe(handler));
            Assert.Null(exception);
        }

        [Fact]
        public void SendUserLoggedOut_CalledMultipleTimes_ShouldInvokeSubscriberEachTime()
        {
            int invokeCount = 0;
            Action handler = () => invokeCount++;
            Messenger.UserLoggedOut += handler;
            Messenger.SendUserLoggedOut();
            Messenger.SendUserLoggedOut();
            Messenger.SendUserLoggedOut();
            Assert.Equal(3, invokeCount);
            Messenger.Unsubscribe(handler);
        }

        [Fact]
        public void Unsubscribe_WithOneOfMultipleHandlers_ShouldOnlyRemoveThatHandler()
        {
            int invokeCount = 0;
            Action handler1 = () => invokeCount++;
            Action handler2 = () => invokeCount += 10;
            Messenger.UserLoggedOut += handler1;
            Messenger.UserLoggedOut += handler2;
            Messenger.Unsubscribe(handler1);
            Messenger.SendUserLoggedOut();
            Assert.Equal(10, invokeCount);
            Messenger.Unsubscribe(handler2);
        }

        [Fact]
        public void UserLoggedOut_WhenSubscribed_ShouldAllowResubscription()
        {
            int invokeCount = 0;
            Action handler = () => invokeCount++;
            Messenger.UserLoggedOut += handler;
            Messenger.Unsubscribe(handler);
            Messenger.UserLoggedOut += handler;
            Messenger.SendUserLoggedOut();
            Assert.Equal(1, invokeCount);
            Messenger.Unsubscribe(handler);
        }
    }
}
