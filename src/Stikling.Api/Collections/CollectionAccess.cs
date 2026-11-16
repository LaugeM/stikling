using Microsoft.AspNetCore.Authorization;
using Microsoft.EntityFrameworkCore;
using Stikling.Api.Data;
using Stikling.Api.People;

namespace Stikling.Api.Collections;

/// <summary>
/// The policies for endpoints under <c>/collections/{collectionId}</c>. They check the caller's
/// membership of that collection on every request, so a viewer can't change anything even with a
/// modified app.
/// </summary>
public static class CollectionPolicies
{
    public const string View = "collection:view";
    public const string Help = "collection:help";
    public const string Edit = "collection:edit";
    public const string Manage = "collection:manage";
    public const string Own = "collection:own";

    /// <summary>Where the handler leaves what it found about the caller, for the endpoint to read.</summary>
    private const string RightsKey = "Stikling.MemberRights";

    public static IServiceCollection AddCollectionAuthorization(this IServiceCollection services)
    {
        services.AddScoped<IAuthorizationHandler, CollectionAccessHandler>();
        services.AddAuthorizationBuilder()
            .AddPolicy(View, policy => policy.RequireAuthenticatedUser().AddRequirements(new CollectionAccessRequirement(CollectionRight.View)))
            .AddPolicy(Help, policy => policy.RequireAuthenticatedUser().AddRequirements(new CollectionAccessRequirement(CollectionRight.Help)))
            .AddPolicy(Edit, policy => policy.RequireAuthenticatedUser().AddRequirements(new CollectionAccessRequirement(CollectionRight.Edit)))
            .AddPolicy(Manage, policy => policy.RequireAuthenticatedUser().AddRequirements(new CollectionAccessRequirement(CollectionRight.Manage)))
            .AddPolicy(Own, policy => policy.RequireAuthenticatedUser().AddRequirements(new CollectionAccessRequirement(CollectionRight.Own)));
        return services;
    }

    /// <summary>Whether someone with this role may do what <paramref name="needed"/> allows.</summary>
    public static bool Allows(this MemberRole role, MemberRole needed) =>
        PeopleRules.Rank(role) >= PeopleRules.Rank(needed);

    internal static void SetRights(this HttpContext http, MemberRights rights) => http.Items[RightsKey] = rights;

    /// <summary>
    /// What the policy found out about the caller. Only for endpoints that have one of the
    /// collection policies, which always leave it.
    /// </summary>
    public static MemberRights Rights(this HttpContext http) =>
        http.Items[RightsKey] as MemberRights ?? throw new InvalidOperationException("The endpoint has no collection policy.");
}

public sealed record CollectionAccessRequirement(CollectionRight Needed) : IAuthorizationRequirement;

public sealed class CollectionAccessHandler(CurrentPerson current, StiklingDbContext db)
    : AuthorizationHandler<CollectionAccessRequirement, HttpContext>
{
    protected override async Task HandleRequirementAsync(
        AuthorizationHandlerContext context, CollectionAccessRequirement requirement, HttpContext http)
    {
        // Handlers still run after RequireAuthenticatedUser has failed, so a request without a
        // valid token gets here too. It gets a 401 from that failure.
        if (context.User.Identity?.IsAuthenticated != true)
            return;

        if (!Guid.TryParse(http.GetRouteValue("collectionId") as string, out var collectionId))
            return;

        if (await current.FindAsync() is not { } person)
            return;

        var rights = await db.Memberships
            .Where(m => m.CollectionId == collectionId && m.PersonId == person.Id)
            .Select(m => new MemberRights(m.PersonId, m.Role, m.ManagesPeople, m.Collection.OwnerPersonId == m.PersonId))
            .SingleOrDefaultAsync();

        if (rights is null)
            return;

        http.SetRights(rights);
        if (rights.Allows(requirement.Needed))
            context.Succeed(requirement);
    }
}
