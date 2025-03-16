using System.Diagnostics;

namespace eShop.Ordering.API.Utilities;

public static class UserDataMasker
{
    /// <summary>
    /// Masks a user ID by showing only the first 4 characters followed by asterisks
    /// </summary>
    /// <param name="userId">The user ID to mask</param>
    /// <returns>Masked user ID</returns>
    public static string MaskUserId(string userId)
    {
        if (string.IsNullOrEmpty(userId))
            return userId;
            
        if (userId.Length > 4)
            return userId.Substring(0, 4) + "****";
        else
            return userId + "****";
    }
    
    /// <summary>
    /// Masks a username by showing only the first 2 characters followed by asterisks
    /// </summary>
    /// <param name="userName">The username to mask</param>
    /// <returns>Masked username</returns>
    public static string MaskUserName(string userName)
    {
        if (string.IsNullOrEmpty(userName))
            return userName;
            
        if (userName.Length > 2)
            return userName.Substring(0, 2) + "****";
        else
            return userName + "****";
    }
}
