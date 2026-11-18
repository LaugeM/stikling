using Stikling.Api.Collections;
using Stikling.Api.Data;
using Stikling.Core.People;

namespace Stikling.Api.Tests;

/// <summary>
/// The device decides what to offer and what to send with <see cref="MemberAccess"/>, and the server
/// decides what to allow with <see cref="MemberRights"/> and <see cref="PeopleRules"/>. They have to say
/// the same, for every role and setting, also the combinations the server never lets anyone have.
/// </summary>
public class MemberAccessTests
{
    private static readonly Guid CallerId = Guid.NewGuid();
    private static readonly Guid TargetId = Guid.NewGuid();

    private static IEnumerable<(MemberRole Role, bool Manages, bool Owner)> Everyone() =>
        from role in new[] { MemberRole.Viewer, MemberRole.Helper, MemberRole.Editor }
        from manages in new[] { false, true }
        from owner in new[] { false, true }
        select (role, manages, owner);

    private static MemberRights Server(Guid id, MemberRole role, bool manages, bool owner) => new(id, role, manages, owner);

    private static MemberAccess Device(Guid id, MemberRole role, bool manages, bool owner) =>
        new(id, Enum.Parse<CollectionRole>(role.ToString()), manages, owner);

    [Fact]
    public void The_rights_agree_for_every_role_and_setting()
    {
        foreach (var (role, manages, owner) in Everyone())
        {
            var server = Server(CallerId, role, manages, owner);
            var device = Device(CallerId, role, manages, owner);
            var which = $"{role}, manages {manages}, owner {owner}";

            Assert.True(server.Allows(CollectionRight.Help) == device.CanLog, which);
            Assert.True(server.Allows(CollectionRight.Edit) == device.CanEdit, which);
            Assert.True(server.Allows(CollectionRight.Manage) == device.CanManage, which);
            Assert.True(server.Allows(CollectionRight.Own) == device.IsOwner, which);
            Assert.True(server.Allows(CollectionRight.Own) == device.CanHandOver, which);
            Assert.True(!server.Allows(CollectionRight.Own) == device.CanLeave, which);

            foreach (var managesPeople in new[] { false, true })
            {
                Assert.True(
                    (server.Allows(CollectionRight.Manage) && PeopleRules.CanInvite(server, managesPeople)) == device.CanInvite(managesPeople),
                    $"{which}, invites with manages {managesPeople}");
            }
        }
    }

    [Fact]
    public void Changing_and_removing_people_agree_for_every_caller_and_target()
    {
        foreach (var (role, manages, owner) in Everyone())
        {
            foreach (var (targetRole, targetManages, targetOwner) in Everyone())
            {
                // Another person, and the caller's own row
                foreach (var targetId in new[] { TargetId, CallerId })
                {
                    var which = $"{role}/{manages}/{owner} on {targetRole}/{targetManages}/{targetOwner}, same person {targetId == CallerId}";
                    var server = Server(CallerId, role, manages, owner);
                    var device = Device(CallerId, role, manages, owner);
                    var serverTarget = Server(targetId, targetRole, targetManages, targetOwner);
                    var deviceTarget = Device(targetId, targetRole, targetManages, targetOwner);

                    Assert.True(PeopleRules.CanRemove(server, serverTarget) == device.CanRemove(deviceTarget), $"remove, {which}");
                    foreach (var newManagesPeople in new[] { false, true })
                    {
                        Assert.True(
                            PeopleRules.CanChange(server, serverTarget, newManagesPeople) == device.CanChange(deviceTarget, newManagesPeople),
                            $"change to manages {newManagesPeople}, {which}");
                    }
                }
            }
        }
    }

    [Fact]
    public void A_helpers_own_entry_and_the_servers_ownership_rule_agree()
    {
        var helper = Device(CallerId, MemberRole.Helper, false, false);

        Assert.True(helper.CanChangeEntry(CallerId));
        Assert.Null(PeopleRules.HelperProblem("careLogs", System.Text.Json.JsonSerializer.SerializeToElement(new { id = Guid.NewGuid() }), exists: true, existingCreatedBy: CallerId, CallerId));

        Assert.False(helper.CanChangeEntry(TargetId));
        Assert.Equal(
            HelperSync.HelperOwnershipProblem,
            PeopleRules.HelperProblem("careLogs", System.Text.Json.JsonSerializer.SerializeToElement(new { id = Guid.NewGuid() }), exists: true, existingCreatedBy: TargetId, CallerId));
    }

    [Fact]
    public void The_text_the_server_sends_for_a_refused_helper_record_is_what_the_device_looks_for()
    {
        Assert.Equal("A helper can't change this kind of record.", PeopleRules.HelperKindProblem);
        Assert.Equal("Only the person who added it can change it.", PeopleRules.HelperOwnershipProblem);
        Assert.Equal(HelperSync.Kinds, PeopleRules.HelperKinds);
    }
}
