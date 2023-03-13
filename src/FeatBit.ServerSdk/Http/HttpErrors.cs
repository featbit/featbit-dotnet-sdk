namespace FeatBit.Sdk.Server.Http;

internal static class HttpErrors
{
    /// <summary>
    /// Returns true if this type of error could be expected to eventually resolve itself,
    /// or false if it indicates a configuration problem or client logic error such that the
    /// client should give up on making any further requests.
    /// </summary>
    /// <param name="status">a status code</param>
    /// <returns>true if retrying is appropriate</returns>
    public static bool IsRecoverable(int status)
    {
        if (status is >= 400 and <= 499)
        {
            return status is 400 or 408 or 429;
        }

        return true;
    }
}