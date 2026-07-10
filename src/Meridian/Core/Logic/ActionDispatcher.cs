using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;

namespace Meridian.Core.Logic;

/// <summary>
/// Validated dispatcher that turns a named verb + string arguments into an <see cref="IGameAction"/>
/// and executes it against an <see cref="IActionContext"/> (§10.2 writer→game-actions contract, §10.4).
/// The verb table is a code-registered whitelist, so narrative/interactable content can trigger
/// effects safely by name: unknown verbs, wrong arity, and unparseable numeric arguments are rejected
/// with a precise, actionable error instead of ever throwing.
/// </summary>
public sealed class ActionDispatcher
{
    private readonly Dictionary<string, VerbSpec> _verbs = new(StringComparer.OrdinalIgnoreCase);
    private readonly List<string> _order = new();

    /// <summary>Creates a dispatcher pre-populated with the standard verb table.</summary>
    public ActionDispatcher()
    {
        RegisterDefaults();
    }

    /// <summary>All registered verb names, in registration order.</summary>
    public IEnumerable<string> RegisteredVerbs => _order;

    /// <summary>
    /// Human-readable usage lines for every verb (e.g. "give_item &lt;id&gt; &lt;count&gt;"), for the
    /// content validator and console help.
    /// </summary>
    public IEnumerable<string> UsageLines => _order.Select(v => _verbs[v].Usage);

    /// <summary>Returns the usage string for a verb, or null when the verb is not registered.</summary>
    public string? GetUsage(string verb)
    {
        if (verb is not null && _verbs.TryGetValue(verb, out var spec))
        {
            return spec.Usage;
        }

        return null;
    }

    /// <summary>
    /// Parses and executes a verb. Returns true when the verb was recognised, its arguments validated,
    /// and the action executed. Returns false — with <paramref name="error"/> populated — for an unknown
    /// verb, wrong argument count, or an unparseable numeric argument. Never throws.
    /// </summary>
    /// <remarks>
    /// A true result means the action was dispatched, not that its underlying effect succeeded (e.g.
    /// <c>give_item</c> may still be refused by a full inventory — that is a runtime outcome, not a
    /// dispatch failure).
    /// </remarks>
    public bool TryDispatch(string verb, IReadOnlyList<string> args, IActionContext context, out string error)
    {
        if (context is null)
        {
            error = "no action context was supplied.";
            return false;
        }

        if (string.IsNullOrWhiteSpace(verb))
        {
            error = "no action verb was supplied.";
            return false;
        }

        if (!_verbs.TryGetValue(verb, out var spec))
        {
            error = $"unknown action verb '{verb}'. Known verbs: {string.Join(", ", RegisteredVerbs)}.";
            return false;
        }

        args ??= Array.Empty<string>();

        var result = spec.Parse(args);
        if (result.Action is null)
        {
            error = result.Error ?? $"invalid arguments. Usage: {spec.Usage}";
            return false;
        }

        result.Action.Execute(context);
        error = string.Empty;
        return true;
    }

    private void RegisterDefaults()
    {
        Register("give_item", "give_item <id> <count>", args =>
        {
            if (!TryFixedArity(args, 2, "give_item <id> <count>", out var err))
            {
                return err;
            }

            if (!TryParseInt(args[1], "count", out int count, out var countErr))
            {
                return countErr;
            }

            return ParseResult.Ok(new GiveItemAction(args[0], count));
        });

        Register("remove_item", "remove_item <id> <count>", args =>
        {
            if (!TryFixedArity(args, 2, "remove_item <id> <count>", out var err))
            {
                return err;
            }

            if (!TryParseInt(args[1], "count", out int count, out var countErr))
            {
                return countErr;
            }

            return ParseResult.Ok(new RemoveItemAction(args[0], count));
        });

        Register("grant_xp", "grant_xp <amount>", args =>
        {
            if (!TryFixedArity(args, 1, "grant_xp <amount>", out var err))
            {
                return err;
            }

            if (!TryParseInt(args[0], "amount", out int amount, out var amountErr))
            {
                return amountErr;
            }

            return ParseResult.Ok(new GrantXpAction(amount));
        });

        Register("set_flag", "set_flag <id> <true|false>", args =>
        {
            if (!TryFixedArity(args, 2, "set_flag <id> <true|false>", out var err))
            {
                return err;
            }

            if (!TryParseBool(args[1], out bool value, out var boolErr))
            {
                return boolErr;
            }

            return ParseResult.Ok(new SetWorldFlagAction(args[0], value));
        });

        Register("play_cue", "play_cue <id>", args =>
        {
            if (!TryFixedArity(args, 1, "play_cue <id>", out var err))
            {
                return err;
            }

            return ParseResult.Ok(new PlaySoundCueAction(args[0]));
        });

        Register("notify", "notify <message...>", args =>
        {
            if (args.Count < 1)
            {
                return ParseResult.Fail("expected a message. Usage: notify <message...>");
            }

            // Joins all remaining args so multi-word messages need no quoting.
            return ParseResult.Ok(new ShowNotificationAction(string.Join(' ', args)));
        });

        Register("teleport_player", "teleport_player <x> <y> <z>", args =>
        {
            if (!TryFixedArity(args, 3, "teleport_player <x> <y> <z>", out var err))
            {
                return err;
            }

            if (!TryParseFloat(args[0], "x", out float x, out var xErr))
            {
                return xErr;
            }

            if (!TryParseFloat(args[1], "y", out float y, out var yErr))
            {
                return yErr;
            }

            if (!TryParseFloat(args[2], "z", out float z, out var zErr))
            {
                return zErr;
            }

            return ParseResult.Ok(new TeleportPlayerAction(x, y, z));
        });

        Register("start_quest", "start_quest <id>", args =>
        {
            if (!TryFixedArity(args, 1, "start_quest <id>", out var err))
            {
                return err;
            }

            return ParseResult.Ok(new StartQuestAction(args[0]));
        });

        Register("spawn_scene", "spawn_scene <path> <x> <y> <z>", args =>
        {
            if (!TryFixedArity(args, 4, "spawn_scene <path> <x> <y> <z>", out var err))
            {
                return err;
            }

            if (!TryParseFloat(args[1], "x", out float x, out var xErr))
            {
                return xErr;
            }

            if (!TryParseFloat(args[2], "y", out float y, out var yErr))
            {
                return yErr;
            }

            if (!TryParseFloat(args[3], "z", out float z, out var zErr))
            {
                return zErr;
            }

            return ParseResult.Ok(new SpawnSceneAction(args[0], x, y, z));
        });
    }

    private void Register(string verb, string usage, Func<IReadOnlyList<string>, ParseResult> parse)
    {
        if (!_verbs.ContainsKey(verb))
        {
            _order.Add(verb);
        }

        _verbs[verb] = new VerbSpec(verb, usage, parse);
    }

    // --- Argument parsing helpers (all return an actionable error string on failure) ---

    private static bool TryFixedArity(IReadOnlyList<string> args, int expected, string usage, out ParseResult error)
    {
        if (args.Count != expected)
        {
            error = ParseResult.Fail(
                $"expected {expected} argument{(expected == 1 ? "" : "s")} but got {args.Count}. Usage: {usage}");
            return false;
        }

        error = default;
        return true;
    }

    private static bool TryParseInt(string raw, string paramName, out int value, out ParseResult error)
    {
        if (int.TryParse(raw, NumberStyles.Integer, CultureInfo.InvariantCulture, out value))
        {
            error = default;
            return true;
        }

        error = ParseResult.Fail($"could not parse <{paramName}> '{raw}' as an integer.");
        return false;
    }

    private static bool TryParseFloat(string raw, string paramName, out float value, out ParseResult error)
    {
        if (float.TryParse(raw, NumberStyles.Float, CultureInfo.InvariantCulture, out value))
        {
            error = default;
            return true;
        }

        error = ParseResult.Fail($"could not parse <{paramName}> '{raw}' as a number.");
        return false;
    }

    private static bool TryParseBool(string raw, out bool value, out ParseResult error)
    {
        switch (raw?.Trim().ToLowerInvariant())
        {
            case "true":
            case "1":
            case "yes":
            case "on":
                value = true;
                error = default;
                return true;
            case "false":
            case "0":
            case "no":
            case "off":
                value = false;
                error = default;
                return true;
            default:
                value = false;
                error = ParseResult.Fail($"could not parse '{raw}' as a boolean (expected true|false).");
                return false;
        }
    }

    /// <summary>Outcome of parsing verb arguments: an action on success, or an error message.</summary>
    private readonly record struct ParseResult(IGameAction? Action, string? Error)
    {
        public static ParseResult Ok(IGameAction action) => new(action, null);

        public static ParseResult Fail(string message) => new(null, message);
    }

    private sealed record VerbSpec(string Verb, string Usage, Func<IReadOnlyList<string>, ParseResult> Parse);
}
