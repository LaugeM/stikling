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
    public const string Edit = "collection:edit";

    public static IServiceCollection AddCollectionAuthorization(this IServiceCollection services)
    {
        services.AddScoped<IAuthorizationHandler, CollectionAccessHandler>();
        services.AddAuthorizationBuilder()
            .AddPolicy(View, policy => policy.RequireAuthenticatedUser().AddRequirements(new CollectionAccessRequirement(MemberRole.Viewer)))
            .AddPolicy(Edit, policy => policy.RequireAuthenticatedUser().AddRequirements(new CollectionAccessRequirement(MemberRole.Editor)));
        return services;
    }

    /// <summary>Whether someone with this role may do what <paramref name="needed"/> allows.</summary>
    public static bool Allows(this MemberRole role, MemberRole needed) =>
        needed == MemberRole.Viewer || role == MemberRole.Editor;
}

public sealed record CollectionAccessRequirement(MemberRole Needed) : IAuthorizationRequirement;

public sealed class CollectionAccessHandler(CurrentPerson current, StiklingDbContext db)
    : AuthorizationHandler<CollectionAccessRequirement, HttpContext>
{
    protected override async Task HandleRequirementAsync(
        AuthorizationHandlerContext context, CollectionAccessRequirement requirement, HttpContext http)
    {
        if (!Guid.TryParse(http.GetRouteValue("collectionId") as string, out var collectionId))
            return;

        if (await current.FindAsync() is not { } person)
            return;

        var role = await db.Memberships
            .Where(m => m.CollectionId == collectionId && m.PersonId == person.Id)
            .Select(m => (MemberRole?)m.Role)
            .SingleOrDefaultAsync();

        if (role is { } found && found.Allows(requirement.Needed))
            context.Succeed(requirement);
    }
}
