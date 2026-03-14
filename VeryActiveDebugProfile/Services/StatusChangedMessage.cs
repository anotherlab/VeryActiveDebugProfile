using CommunityToolkit.Mvvm.Messaging.Messages;

namespace VeryActiveDebugProfile.Services;

public sealed class StatusChangedMessage(string value) : ValueChangedMessage<string>(value) { }
