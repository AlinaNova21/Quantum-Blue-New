using System.Linq;
using Content.Server.Administration;
using Content.Server.Clothing.Systems;
using Content.Shared.Administration;
using Content.Shared.Clothing.Components;
using Content.Shared.Inventory;
using Robust.Shared.Console;
using Robust.Shared.Prototypes;

namespace Content.Server._QB.Administration.Commands;

/// <summary>
/// Turns an already spawned piece of clothing (e.g. an admin satchel) into chameleon clothing,
/// so its appearance can be changed through the "Chameleon" verb.
/// </summary>
[AdminCommand(AdminFlags.Fun)]
public sealed class ChameleonifyCommand : LocalizedEntityCommands
{
    [Dependency] private readonly IPrototypeManager _proto = default!;
    [Dependency] private readonly ChameleonClothingSystem _chameleon = default!;

    private static readonly SlotFlags[] IgnoredSlots = [SlotFlags.NONE, SlotFlags.PREVENTEQUIP, SlotFlags.All, SlotFlags.WITHOUT_POCKET];

    public override string Command => "chameleonify";
    public override string Description => Loc.GetString("cmd-chameleonify-desc");
    public override string Help => Loc.GetString("cmd-chameleonify-help", ("command", Command));

    public override void Execute(IConsoleShell shell, string argStr, string[] args)
    {
        if (args.Length is < 1 or > 3)
        {
            shell.WriteError(Loc.GetString("shell-wrong-arguments-number"));
            return;
        }

        if (!NetEntity.TryParse(args[0], out var netEnt) || !EntityManager.TryGetEntity(netEnt, out var uid))
        {
            shell.WriteError(Loc.GetString("shell-invalid-entity-uid", ("uid", args[0])));
            return;
        }

        if (!EntityManager.TryGetComponent<ClothingComponent>(uid, out var clothing))
        {
            shell.WriteError(Loc.GetString("cmd-chameleonify-not-clothing", ("entity", uid.Value)));
            return;
        }

        // The chameleon menu lists options for exactly one slot, so pick one if the item fits several.
        SlotFlags slot;
        if (args.Length >= 3)
        {
            if (!Enum.TryParse(args[2], true, out slot) || !Enum.IsDefined(slot) || IgnoredSlots.Contains(slot))
            {
                shell.WriteError(Loc.GetString("cmd-chameleonify-bad-slot", ("slot", args[2])));
                return;
            }
        }
        else
        {
            slot = Enum.GetValues<SlotFlags>().FirstOrDefault(s => !IgnoredSlots.Contains(s) && clothing.Slots.HasFlag(s));
            if (slot == SlotFlags.NONE)
            {
                shell.WriteError(Loc.GetString("cmd-chameleonify-not-clothing", ("entity", uid.Value)));
                return;
            }
        }

        // Default to looking like itself, so nothing changes until a new appearance is picked.
        var defaultProto = args.Length >= 2 ? args[1] : EntityManager.GetComponent<MetaDataComponent>(uid.Value).EntityPrototype?.ID;
        if (defaultProto == null
            || !_proto.TryIndex<EntityPrototype>(defaultProto, out var proto)
            || !_chameleon.IsValidTarget(proto, slot))
        {
            shell.WriteError(Loc.GetString("cmd-chameleonify-bad-default", ("prototype", defaultProto ?? "null"), ("slot", slot)));
            return;
        }

        _chameleon.MakeChameleon(uid.Value, slot, defaultProto);
        shell.WriteLine(Loc.GetString("cmd-chameleonify-success", ("entity", uid.Value), ("slot", slot)));
    }

    public override CompletionResult GetCompletion(IConsoleShell shell, string[] args)
    {
        return args.Length switch
        {
            1 => CompletionResult.FromHint(Loc.GetString("cmd-chameleonify-hint-entity")),
            2 => CompletionResult.FromHintOptions(
                _chameleon.GetValidTargets(SlotFlags.BACK).Select(p => p.Id),
                Loc.GetString("cmd-chameleonify-hint-default")),
            3 => CompletionResult.FromHintOptions(
                Enum.GetNames<SlotFlags>().Where(s => !IgnoredSlots.Contains(Enum.Parse<SlotFlags>(s))),
                Loc.GetString("cmd-chameleonify-hint-slot")),
            _ => CompletionResult.Empty,
        };
    }
}
