using Microsoft.AspNetCore.Mvc.ViewFeatures;

namespace MoffDiscordLink.Extensions;

public static class StatusMessageHelper
{
    public const string KeyMessage = "StatusMessage";
    public const string KeyType = "StatusMessageType";

    extension(ITempDataDictionary tempData)
    {
        public void SetStatusError(string message) => tempData.SetStatusMessage(message, Type.Error);
        public void SetStatusInformation(string message) => tempData.SetStatusMessage(message, Type.Information);

        public void SetStatusMessage(string message, Type type)
        {
            tempData[KeyMessage] = message;
            tempData[KeyType] = type;
        }
    }

    public enum Type
    {
        Information,
        Error,
    }
}
