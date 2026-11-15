using System.Security.Claims;

namespace WatchSphere.API.Extensions
{
    public static class ClaimsPrincipalExtensions
    {
        /// <summary>
        /// Reads the caller's id from the token. Returns null instead of
        /// throwing when the claim is missing or malformed - the previous
        /// int.Parse(...!) pattern turned a bad token into a 500.
        /// </summary>
        public static int? GetUserId(this ClaimsPrincipal principal)
        {
            var raw = principal.FindFirst(ClaimTypes.NameIdentifier)?.Value;

            return int.TryParse(raw, out var id) ? id : null;
        }

        public static bool IsAdmin(this ClaimsPrincipal principal)
        {
            return principal.IsInRole("Admin");
        }
    }
}
